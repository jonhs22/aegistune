using System.Runtime.Versioning;
using AegisTune.Core;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows10.0.10240.0")]
public sealed class WindowsInstalledApplicationInventoryService : IInstalledApplicationInventoryService
{
    private static readonly char[] InvalidDirectoryNameCharacters = Path.GetInvalidFileNameChars();

    public Task<AppInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            DateTimeOffset scannedAt = DateTimeOffset.Now;

            try
            {
                var applications = new List<InstalledApplicationRecord>();
                var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var warnings = new List<string>();

                foreach (RegistryView view in GetRegistryViews())
                {
                    EnumerateRegistryApplications(applications, seenEntries, warnings, RegistryHive.LocalMachine, view, "All users", cancellationToken);
                    EnumerateRegistryApplications(applications, seenEntries, warnings, RegistryHive.CurrentUser, view, "Current user", cancellationToken);
                }

                EnumeratePackagedApplications(applications, seenEntries, warnings);

                InstalledApplicationRecord[] orderedApps = applications
                    .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(app => app.DisplayVersion, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                InstalledApplicationRecord[] enrichedApps = orderedApps
                    .Select(app => EnrichResidueEvidence(app, warnings, cancellationToken))
                    .ToArray();

                string? warningMessage = warnings.Count == 0
                    ? null
                    : enrichedApps.Length > 0
                        ? $"Installed app inventory completed with {warnings.Count:N0} skipped entr{(warnings.Count == 1 ? "y" : "ies")}."
                        : $"Installed app inventory failed after {warnings.Count:N0} skipped entr{(warnings.Count == 1 ? "y" : "ies")}.";

                return new AppInventorySnapshot(enrichedApps, scannedAt, warningMessage);
            }
            catch (Exception ex)
            {
                return new AppInventorySnapshot(Array.Empty<InstalledApplicationRecord>(), scannedAt, $"Installed app inventory failed: {ex.Message}");
            }
        }, cancellationToken);

    private static IEnumerable<RegistryView> GetRegistryViews()
    {
        if (Environment.Is64BitOperatingSystem)
        {
            return [RegistryView.Registry64, RegistryView.Registry32];
        }

        return [RegistryView.Default];
    }

    private static void EnumerateRegistryApplications(
        ICollection<InstalledApplicationRecord> applications,
        ISet<string> seenEntries,
        ICollection<string> warnings,
        RegistryHive hive,
        RegistryView view,
        string scopeLabel,
        CancellationToken cancellationToken)
    {
        const string uninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

        RegistryKey? uninstallKey = null;
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            uninstallKey = baseKey.OpenSubKey(uninstallPath);
        }
        catch (Exception ex)
        {
            warnings.Add($"{hive} {GetViewLabel(view)} uninstall hive could not be opened: {ex.Message}");
        }

        if (uninstallKey is null)
        {
            return;
        }

        using (uninstallKey)
        {
            foreach (string subKeyName in uninstallKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using RegistryKey? appKey = uninstallKey.OpenSubKey(subKeyName);
                    if (appKey is null)
                    {
                        continue;
                    }

                    string? displayName = appKey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    string registryKeyPath = $@"{hive}\{uninstallPath}\{subKeyName}";
                    string displayVersion = appKey.GetValue("DisplayVersion")?.ToString() ?? string.Empty;
                    string publisher = appKey.GetValue("Publisher")?.ToString() ?? string.Empty;
                    string? installLocation = appKey.GetValue("InstallLocation")?.ToString();
                    string? uninstallCommand = appKey.GetValue("UninstallString")?.ToString();
                    string? quietUninstallCommand = appKey.GetValue("QuietUninstallString")?.ToString();
                    string? resolvedUninstallTargetPath = CommandPathResolver.ResolveTargetPath(quietUninstallCommand ?? uninstallCommand);
                    bool uninstallTargetExists = resolvedUninstallTargetPath is not null && File.Exists(resolvedUninstallTargetPath);
                    bool installLocationExists = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation);
                    long? estimatedSizeBytes = ParseEstimatedSizeBytes(appKey.GetValue("EstimatedSize"));

                    string identity = string.Join("|", displayName, displayVersion, publisher, registryKeyPath);
                    if (!seenEntries.Add(identity))
                    {
                        continue;
                    }

                    applications.Add(new InstalledApplicationRecord(
                        displayName,
                        displayVersion,
                        publisher,
                        InstalledApplicationSource.DesktopRegistry,
                        $"{scopeLabel} ({GetViewLabel(view)})",
                        registryKeyPath,
                        installLocation,
                        installLocationExists,
                        quietUninstallCommand ?? uninstallCommand,
                        resolvedUninstallTargetPath,
                        uninstallTargetExists,
                        estimatedSizeBytes));
                }
                catch (Exception ex)
                {
                    warnings.Add($"{hive} {GetViewLabel(view)} uninstall entry '{subKeyName}' was skipped: {ex.Message}");
                }
            }
        }
    }

    private static void EnumeratePackagedApplications(
        ICollection<InstalledApplicationRecord> applications,
        ISet<string> seenEntries,
        ICollection<string> warnings)
    {
        PackageManager packageManager = new();

        foreach (Windows.ApplicationModel.Package package in packageManager.FindPackagesForUser(string.Empty))
        {
            try
            {
                if (package.IsFramework)
                {
                    continue;
                }

                string displayName = package.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = package.Id.Name;
                }

                string version = $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}";
                string publisher = package.PublisherDisplayName;
                string keyPath = package.Id.FullName;
                string identity = string.Join("|", displayName, version, publisher, keyPath);

                if (!seenEntries.Add(identity))
                {
                    continue;
                }

                string? installLocation = null;
                bool installLocationExists = false;

                try
                {
                    installLocation = package.InstalledLocation?.Path;
                    installLocationExists = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation);
                }
                catch
                {
                    installLocation = null;
                    installLocationExists = false;
                }

                applications.Add(new InstalledApplicationRecord(
                    displayName,
                    version,
                    publisher,
                    InstalledApplicationSource.Packaged,
                    "Current user",
                    keyPath,
                    installLocation,
                    installLocationExists,
                    UninstallCommand: null,
                    ResolvedUninstallTargetPath: null,
                    UninstallTargetExists: false,
                    EstimatedSizeBytes: null));
            }
            catch (Exception ex)
            {
                string packageIdentity = package.Id.FullName;
                warnings.Add($"Packaged app '{packageIdentity}' was skipped: {ex.Message}");
            }
        }
    }

    private static InstalledApplicationRecord EnrichResidueEvidence(
        InstalledApplicationRecord application,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        if (!ShouldInspectResidue(application))
        {
            return application;
        }

        try
        {
            IReadOnlyList<ApplicationResidueRecord> residueEvidence = DiscoverResidueEvidence(application, cancellationToken);
            return residueEvidence.Count == 0
                ? application
                : application with { ResidueEvidence = residueEvidence };
        }
        catch (Exception ex)
        {
            warnings.Add($"Residue review for '{application.DisplayName}' was skipped: {ex.Message}");
            return application;
        }
    }

    private static bool ShouldInspectResidue(InstalledApplicationRecord application) =>
        application.Source == InstalledApplicationSource.DesktopRegistry
        && (!application.UninstallTargetExists || application.HasBrokenInstallEvidence);

    private static IReadOnlyList<ApplicationResidueRecord> DiscoverResidueEvidence(
        InstalledApplicationRecord application,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> candidatePaths = new(StringComparer.OrdinalIgnoreCase);

        if (application.InstallLocationExists && !application.UninstallTargetExists && !string.IsNullOrWhiteSpace(application.InstallLocation))
        {
            candidatePaths[application.InstallLocation] = "Install location";
        }

        string[] appNameVariants = BuildDirectoryNameVariants(application.DisplayName, application.Publisher);
        string[] publisherVariants = BuildPublisherVariants(application.Publisher);

        foreach ((string root, string scopeLabel) in GetResidueRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (string appVariant in appNameVariants)
            {
                candidatePaths.TryAdd(Path.Combine(root, appVariant), scopeLabel);
            }

            foreach (string publisherVariant in publisherVariants)
            {
                foreach (string appVariant in appNameVariants)
                {
                    candidatePaths.TryAdd(Path.Combine(root, publisherVariant, appVariant), scopeLabel);
                }
            }
        }

        List<ApplicationResidueRecord> residueEvidence = [];
        foreach ((string path, string scopeLabel) in candidatePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(path))
            {
                continue;
            }

            residueEvidence.Add(BuildResidueRecord(path, scopeLabel, cancellationToken));
        }

        return residueEvidence
            .OrderByDescending(entry => entry.SizeBytes)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
    }

    private static IEnumerable<(string Root, string ScopeLabel)> GetResidueRoots()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return (programData, "ProgramData");
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return (localAppData, "Local AppData");
        }

        if (!string.IsNullOrWhiteSpace(roamingAppData))
        {
            yield return (roamingAppData, "Roaming AppData");
        }
    }

    private static string[] BuildDirectoryNameVariants(string displayName, string publisher)
    {
        HashSet<string> variants = new(StringComparer.OrdinalIgnoreCase);

        AddDirectoryNameVariant(variants, displayName);
        AddDirectoryNameVariant(variants, RemoveTrailingVersion(displayName));
        AddDirectoryNameVariant(variants, RemovePublisherPrefix(displayName, publisher));
        AddDirectoryNameVariant(variants, RemoveTrailingVersion(RemovePublisherPrefix(displayName, publisher)));

        return variants.ToArray();
    }

    private static string[] BuildPublisherVariants(string publisher)
    {
        HashSet<string> variants = new(StringComparer.OrdinalIgnoreCase);
        AddDirectoryNameVariant(variants, publisher);
        return variants.ToArray();
    }

    private static void AddDirectoryNameVariant(ISet<string> variants, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string sanitized = new(value
            .Where(character => Array.IndexOf(InvalidDirectoryNameCharacters, character) < 0)
            .ToArray());
        sanitized = sanitized.Trim().Trim('.', '-', '_');

        if (sanitized.Length >= 3)
        {
            variants.Add(sanitized);
        }
    }

    private static string RemovePublisherPrefix(string displayName, string publisher)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(publisher))
        {
            return displayName;
        }

        return displayName.StartsWith(publisher, StringComparison.OrdinalIgnoreCase)
            ? displayName[publisher.Length..].TrimStart(' ', '-', '_')
            : displayName;
    }

    private static string RemoveTrailingVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            return value;
        }

        int endIndex = parts.Length;
        while (endIndex > 1 && LooksLikeVersionToken(parts[endIndex - 1]))
        {
            endIndex--;
        }

        return endIndex == parts.Length
            ? value
            : string.Join(' ', parts.Take(endIndex));
    }

    private static bool LooksLikeVersionToken(string token)
    {
        string normalized = token.Trim('(', ')', '[', ']', '-', '_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Length == 4 && normalized.All(char.IsDigit))
        {
            return true;
        }

        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return normalized.Length > 0
            && normalized.All(character => char.IsDigit(character) || character == '.');
    }

    private static ApplicationResidueRecord BuildResidueRecord(
        string path,
        string scopeLabel,
        CancellationToken cancellationToken)
    {
        long sizeBytes = 0;
        int fileCount = 0;
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(path);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDirectory = pendingDirectories.Pop();

            try
            {
                foreach (string file in Directory.EnumerateFiles(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        FileInfo info = new(file);
                        sizeBytes += info.Length;
                        fileCount++;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            try
            {
                foreach (string subDirectory in Directory.EnumerateDirectories(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingDirectories.Push(subDirectory);
                }
            }
            catch
            {
            }
        }

        return new ApplicationResidueRecord(path, scopeLabel, sizeBytes, fileCount);
    }

    private static long? ParseEstimatedSizeBytes(object? value)
    {
        long? sizeKb = value switch
        {
            int intValue => intValue,
            long longValue => longValue,
            string stringValue when long.TryParse(stringValue, out long parsed) => parsed,
            _ => null
        };

        return sizeKb is > 0 ? sizeKb.Value * 1024 : null;
    }

    private static string GetViewLabel(RegistryView view) => view switch
    {
        RegistryView.Registry64 => "64-bit",
        RegistryView.Registry32 => "32-bit",
        _ => "default"
    };
}
