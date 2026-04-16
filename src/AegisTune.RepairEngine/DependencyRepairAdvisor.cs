using System.Text.RegularExpressions;
using AegisTune.Core;

namespace AegisTune.RepairEngine;

public static partial class DependencyRepairAdvisor
{
    private static readonly HashSet<string> DirectXLegacyDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "d3dx9_43.dll",
        "d3dcompiler_43.dll",
        "xinput1_3.dll",
        "xaudio2_7.dll"
    };

    private static readonly HashSet<string> UniversalCrtDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "api-ms-win-crt-runtime-l1-1-0.dll",
        "api-ms-win-crt-heap-l1-1-0.dll",
        "api-ms-win-crt-stdio-l1-1-0.dll",
        "ucrtbase.dll"
    };

    private static readonly HashSet<string> GenericCrashModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "kernel32.dll",
        "kernelbase.dll",
        "ntdll.dll",
        "user32.dll",
        "gdi32.dll"
    };

    public static IReadOnlyList<RepairCandidateRecord> BuildCandidates(
        AppInventorySnapshot appInventory,
        IReadOnlyList<DependencyRepairSignal> signals)
    {
        return signals
            .Select(signal => BuildCandidate(appInventory, signal))
            .Where(candidate => candidate is not null)
            .Cast<RepairCandidateRecord>()
            .GroupBy(
                candidate => $"{candidate.Title}|{candidate.SourceLocation}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<RepairCandidateRecord> BuildManualCandidates(
        AppInventorySnapshot appInventory,
        string rawInput,
        DateTimeOffset observedAt)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return Array.Empty<RepairCandidateRecord>();
        }

        return BuildCandidates(appInventory, ExtractManualSignals(rawInput, observedAt));
    }

    private static RepairCandidateRecord? BuildCandidate(
        AppInventorySnapshot appInventory,
        DependencyRepairSignal signal)
    {
        string dependencyName = Normalize(signal.DependencyName);
        bool evidenceLooksMissing = EvidenceLooksLikeMissingDependency(signal.EvidenceMessage);
        bool looksKnownFamily = IsKnownDependencyFamily(dependencyName);

        if (!looksKnownFamily && (!evidenceLooksMissing || GenericCrashModules.Contains(dependencyName)))
        {
            return null;
        }

        InstalledApplicationRecord? matchedApplication = MatchApplication(appInventory, signal);
        string appLabel = matchedApplication?.DisplayName
            ?? ExtractApplicationLabel(signal.ApplicationPath)
            ?? signal.ApplicationName
            ?? "Affected application";
        bool requiresAdministrator = matchedApplication?.ScopeLabel.Contains("All users", StringComparison.OrdinalIgnoreCase) == true;
        RepairResourceLink? officialResource = ResolveOfficialResource(dependencyName);

        return new RepairCandidateRecord(
            $"Dependency repair review: {appLabel}",
            "Dependency",
            RiskLevel.Review,
            requiresAdministrator,
            BuildEvidenceSummary(signal, appLabel, dependencyName),
            BuildProposedAction(matchedApplication, signal, dependencyName),
            string.IsNullOrWhiteSpace(signal.ApplicationPath)
                ? $"{signal.EvidenceSource} • {dependencyName}"
                : signal.ApplicationPath!,
            appLabel,
            signal.ApplicationPath,
            !string.IsNullOrWhiteSpace(signal.ApplicationPath) && File.Exists(signal.ApplicationPath),
            matchedApplication?.InstallLocation,
            matchedApplication?.InstallLocationExists == true,
            matchedApplication?.UninstallCommand,
            matchedApplication?.ResolvedUninstallTargetPath,
            matchedApplication?.UninstallTargetExists == true,
            ResidueFolderPath: null,
            ResidueFolderExists: false,
            ResidueSummary: null,
            OfficialResourceTitle: officialResource?.Title,
            OfficialResourceLabel: officialResource?.LinkLabel,
            OfficialResourceUri: officialResource?.ResourceUri);
    }

    private static string BuildEvidenceSummary(
        DependencyRepairSignal signal,
        string appLabel,
        string dependencyName)
    {
        string observedAtLabel = signal.ObservedAt.LocalDateTime.ToString("g");
        string message = Normalize(signal.EvidenceMessage);
        if (message.Length > 180)
        {
            message = $"{message[..177]}...";
        }

        return $"{signal.EvidenceSource} reported dependency evidence for '{dependencyName}' in {appLabel} at {observedAtLabel}. {message}";
    }

    private static string BuildProposedAction(
        InstalledApplicationRecord? matchedApplication,
        DependencyRepairSignal signal,
        string dependencyName)
    {
        bool adobeContext = IsAdobeContext(matchedApplication, signal);
        string vendorRepairStep = adobeContext
            ? "Repair or reinstall the Adobe app from Creative Cloud or the official Adobe installer only."
            : "Repair or reinstall the affected application from its official vendor installer only.";

        if (IsVisualCppRuntimeDependency(dependencyName) || dependencyName.Equals("Microsoft Visual C++ runtime", StringComparison.OrdinalIgnoreCase))
        {
            return adobeContext
                ? "Install or repair the latest supported Microsoft Visual C++ Redistributable from Microsoft, then repair or reinstall the Adobe app from Creative Cloud. Do not copy runtime DLLs from third-party DLL mirrors."
                : $"Install or repair the latest supported Microsoft Visual C++ Redistributable from Microsoft, then {vendorRepairStep.ToLowerInvariant()} Do not source runtime DLLs from third-party DLL mirrors.";
        }

        if (IsWebView2Dependency(dependencyName))
        {
            return "Repair or reinstall the Microsoft Edge WebView2 Runtime from Microsoft, then repair or reinstall the affected app from its official installer. Do not drop WebView2Loader.dll into the app directory manually.";
        }

        if (DirectXLegacyDlls.Contains(dependencyName))
        {
            return "Install the Microsoft DirectX End-User Runtime (June 2010), then repair or reinstall the affected app from its official installer. Do not fetch DirectX DLLs from third-party DLL download sites.";
        }

        return $"{vendorRepairStep} Restore app-local DLLs only through the vendor package or official repair flow, never from third-party DLL repositories.";
    }

    private static InstalledApplicationRecord? MatchApplication(
        AppInventorySnapshot appInventory,
        DependencyRepairSignal signal)
    {
        if (!string.IsNullOrWhiteSpace(signal.ApplicationPath))
        {
            string applicationPath = NormalizePathSlashes(signal.ApplicationPath!);
            InstalledApplicationRecord? pathMatch = appInventory.Applications.FirstOrDefault(app =>
                !string.IsNullOrWhiteSpace(app.InstallLocation)
                && applicationPath.StartsWith(
                    NormalizePathSlashes(app.InstallLocation!),
                    StringComparison.OrdinalIgnoreCase));

            if (pathMatch is not null)
            {
                return pathMatch;
            }
        }

        if (!string.IsNullOrWhiteSpace(signal.ApplicationName))
        {
            string applicationName = signal.ApplicationName!;
            InstalledApplicationRecord? nameMatch = appInventory.Applications.FirstOrDefault(app =>
                app.DisplayName.Contains(applicationName, StringComparison.OrdinalIgnoreCase)
                || applicationName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase));

            if (nameMatch is not null)
            {
                return nameMatch;
            }
        }

        return null;
    }

    private static bool IsKnownDependencyFamily(string dependencyName) =>
        IsVisualCppRuntimeDependency(dependencyName)
        || IsWebView2Dependency(dependencyName)
        || DirectXLegacyDlls.Contains(dependencyName)
        || dependencyName.Equals("Microsoft Visual C++ runtime", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisualCppRuntimeDependency(string dependencyName)
    {
        if (UniversalCrtDlls.Contains(dependencyName))
        {
            return true;
        }

        string fileName = dependencyName.ToLowerInvariant();
        return fileName.StartsWith("msvcp", StringComparison.Ordinal)
            || fileName.StartsWith("msvcr", StringComparison.Ordinal)
            || fileName.StartsWith("vcruntime", StringComparison.Ordinal)
            || fileName.StartsWith("concrt", StringComparison.Ordinal);
    }

    private static bool IsWebView2Dependency(string dependencyName) =>
        dependencyName.Equals("WebView2Loader.dll", StringComparison.OrdinalIgnoreCase)
        || dependencyName.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase);

    private static RepairResourceLink? ResolveOfficialResource(string dependencyName)
    {
        if (IsVisualCppRuntimeDependency(dependencyName) || dependencyName.Equals("Microsoft Visual C++ runtime", StringComparison.OrdinalIgnoreCase))
        {
            return RepairResourceCatalog.VisualCppRedistributable;
        }

        if (IsWebView2Dependency(dependencyName))
        {
            return RepairResourceCatalog.WebView2Runtime;
        }

        if (DirectXLegacyDlls.Contains(dependencyName))
        {
            return RepairResourceCatalog.DirectXRuntime;
        }

        return null;
    }

    private static bool IsAdobeContext(InstalledApplicationRecord? matchedApplication, DependencyRepairSignal signal)
    {
        if (matchedApplication is not null)
        {
            if (matchedApplication.Publisher.Contains("Adobe", StringComparison.OrdinalIgnoreCase)
                || matchedApplication.DisplayName.Contains("Adobe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return (signal.ApplicationName?.Contains("Adobe", StringComparison.OrdinalIgnoreCase) == true)
            || (signal.ApplicationPath?.Contains(@"\Adobe\", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool EvidenceLooksLikeMissingDependency(string evidenceMessage)
    {
        string message = evidenceMessage.ToLowerInvariant();
        return message.Contains("missing", StringComparison.Ordinal)
            || message.Contains("not found", StringComparison.Ordinal)
            || message.Contains("could not be found", StringComparison.Ordinal)
            || message.Contains("module was not found", StringComparison.Ordinal)
            || message.Contains("failed to load", StringComparison.Ordinal)
            || message.Contains("side-by-side configuration is incorrect", StringComparison.Ordinal)
            || message.Contains("dependent assembly", StringComparison.Ordinal);
    }

    private static IReadOnlyList<DependencyRepairSignal> ExtractManualSignals(
        string rawInput,
        DateTimeOffset observedAt)
    {
        string applicationText = rawInput;
        string? applicationPath = ExtractApplicationPath(rawInput);
        string? applicationName = ExtractApplicationName(rawInput, applicationPath);

        if (rawInput.Contains("side-by-side configuration is incorrect", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new DependencyRepairSignal(
                    "Microsoft Visual C++ runtime",
                    "Manual input",
                    applicationText,
                    observedAt,
                    applicationName,
                    applicationPath)
            ];
        }

        string[] dependencyNames = DllRegex()
            .Matches(rawInput)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dependencyNames.Length == 0)
        {
            return Array.Empty<DependencyRepairSignal>();
        }

        return dependencyNames
            .Select(dependencyName => new DependencyRepairSignal(
                dependencyName,
                "Manual input",
                applicationText,
                observedAt,
                applicationName,
                applicationPath))
            .ToArray();
    }

    private static string? ExtractApplicationLabel(string? applicationPath)
    {
        if (string.IsNullOrWhiteSpace(applicationPath))
        {
            return null;
        }

        string normalizedPath = NormalizePathSlashes(applicationPath);
        string? parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedPath));
        if (!string.IsNullOrWhiteSpace(parentDirectoryName))
        {
            return parentDirectoryName;
        }

        return Path.GetFileNameWithoutExtension(normalizedPath);
    }

    private static string? ExtractApplicationPath(string text)
    {
        Match match = ApplicationPathRegex().Match(text);
        return match.Success ? NormalizePathSlashes(match.Value) : null;
    }

    private static string? ExtractApplicationName(string text, string? applicationPath)
    {
        if (!string.IsNullOrWhiteSpace(applicationPath))
        {
            return Path.GetFileNameWithoutExtension(applicationPath);
        }

        Match adobeMatch = AdobeApplicationRegex().Match(text);
        if (adobeMatch.Success)
        {
            return adobeMatch.Value;
        }

        Match exeMatch = ExeRegex().Match(text);
        return exeMatch.Success
            ? Path.GetFileNameWithoutExtension(exeMatch.Value)
            : null;
    }

    private static string Normalize(string value) =>
        WhitespaceRegex().Replace(value, " ").Trim();

    private static string NormalizePathSlashes(string path) =>
        path.Replace(@"\\", @"\");

    [GeneratedRegex(@"[A-Za-z0-9._-]+\.dll", RegexOptions.IgnoreCase)]
    private static partial Regex DllRegex();

    [GeneratedRegex(@"[A-Z]:\\[^""\r\n]+?\.exe", RegexOptions.IgnoreCase)]
    private static partial Regex ApplicationPathRegex();

    [GeneratedRegex(@"Adobe\s+[A-Za-z0-9 ][A-Za-z0-9 ]*", RegexOptions.IgnoreCase)]
    private static partial Regex AdobeApplicationRegex();

    [GeneratedRegex(@"[A-Za-z0-9._ -]+\.exe", RegexOptions.IgnoreCase)]
    private static partial Regex ExeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
