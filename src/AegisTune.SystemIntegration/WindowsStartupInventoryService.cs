using System.Runtime.Versioning;
using AegisTune.Core;
using Microsoft.Win32;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupInventoryService : IStartupInventoryService
{
    public Task<StartupInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            DateTimeOffset scannedAt = DateTimeOffset.Now;

            try
            {
                var entries = new List<StartupEntryRecord>();
                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (RegistryView view in GetRegistryViews())
                {
                    EnumerateRegistryEntries(entries, seenKeys, RegistryHive.CurrentUser, view, "Current user", cancellationToken);
                    EnumerateRegistryEntries(entries, seenKeys, RegistryHive.LocalMachine, view, "All users", cancellationToken);
                }

                EnumerateStartupFolder(
                    entries,
                    seenKeys,
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "Current user",
                    cancellationToken);
                EnumerateStartupFolder(
                    entries,
                    seenKeys,
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                    "All users",
                    cancellationToken);

                StartupEntryRecord[] orderedEntries = entries
                    .OrderByDescending(entry => entry.IsOrphaned)
                    .ThenByDescending(entry => GetImpactRank(entry.ImpactLevel))
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new StartupInventorySnapshot(orderedEntries, scannedAt);
            }
            catch (Exception ex)
            {
                return new StartupInventorySnapshot(Array.Empty<StartupEntryRecord>(), scannedAt, $"Startup inventory failed: {ex.Message}");
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

    private static void EnumerateRegistryEntries(
        ICollection<StartupEntryRecord> entries,
        ISet<string> seenKeys,
        RegistryHive hive,
        RegistryView view,
        string scopeLabel,
        CancellationToken cancellationToken)
    {
        foreach ((string subKey, string sourceLabel) in new[]
        {
            (@"Software\Microsoft\Windows\CurrentVersion\Run", "Registry Run"),
            (@"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Registry RunOnce")
        })
        {
            cancellationToken.ThrowIfCancellationRequested();

            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? runKey = baseKey.OpenSubKey(subKey);
            if (runKey is null)
            {
                continue;
            }

            foreach (string valueName in runKey.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? command = runKey.GetValue(valueName)?.ToString();
                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                string uniqueKey = $"{hive}:{view}:{subKey}:{valueName}:{command}";
                if (!seenKeys.Add(uniqueKey))
                {
                    continue;
                }

                string? resolvedTargetPath = CommandPathResolver.ResolveTargetPath(command);
                bool targetExists = resolvedTargetPath is not null && File.Exists(resolvedTargetPath);
                bool isOrphaned = resolvedTargetPath is not null && !targetExists;

                entries.Add(new StartupEntryRecord(
                    string.IsNullOrWhiteSpace(valueName) ? "(Default)" : valueName,
                    command,
                    $"{sourceLabel} ({GetViewLabel(view)})",
                    scopeLabel,
                    resolvedTargetPath,
                    targetExists,
                    isOrphaned,
                    DetermineImpactLevel(resolvedTargetPath, targetExists, isOrphaned),
                    StartupEntryOrigin.RegistryValue,
                    $@"{GetHiveLabel(hive)}\{subKey}",
                    valueName,
                    view.ToString(),
                    null,
                    resolvedTargetPath is null ? "Target path requires manual review." : null));
            }
        }
    }

    private static void EnumerateStartupFolder(
        ICollection<StartupEntryRecord> entries,
        ISet<string> seenKeys,
        string folderPath,
        string scopeLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string uniqueKey = $"startup-folder:{filePath}";
            if (!seenKeys.Add(uniqueKey))
            {
                continue;
            }

            string extension = Path.GetExtension(filePath);
            bool targetExists = File.Exists(filePath);
            StartupImpactLevel impactLevel = extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                ? StartupImpactLevel.Review
                : DetermineImpactLevel(filePath, targetExists, isOrphaned: false);

            entries.Add(new StartupEntryRecord(
                Path.GetFileNameWithoutExtension(filePath),
                filePath,
                "Startup folder",
                scopeLabel,
                filePath,
                targetExists,
                IsOrphaned: !targetExists,
                impactLevel,
                StartupEntryOrigin.StartupFolderFile,
                null,
                null,
                null,
                filePath,
                extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                    ? "Shortcut target resolution stays manual in the current milestone."
                    : null));
        }
    }

    private static StartupImpactLevel DetermineImpactLevel(string? resolvedTargetPath, bool targetExists, bool isOrphaned)
    {
        if (isOrphaned || !targetExists || string.IsNullOrWhiteSpace(resolvedTargetPath))
        {
            return StartupImpactLevel.Review;
        }

        string extension = Path.GetExtension(resolvedTargetPath);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            return StartupImpactLevel.Review;
        }

        try
        {
            long fileSize = new FileInfo(resolvedTargetPath).Length;
            if (fileSize >= 150L * 1024 * 1024)
            {
                return StartupImpactLevel.High;
            }

            if (fileSize >= 50L * 1024 * 1024)
            {
                return StartupImpactLevel.Medium;
            }

            return StartupImpactLevel.Low;
        }
        catch (IOException)
        {
            return StartupImpactLevel.Review;
        }
        catch (UnauthorizedAccessException)
        {
            return StartupImpactLevel.Review;
        }
    }

    private static string GetViewLabel(RegistryView view) => view switch
    {
        RegistryView.Registry64 => "64-bit",
        RegistryView.Registry32 => "32-bit",
        _ => "default"
    };

    private static string GetHiveLabel(RegistryHive hive) => hive switch
    {
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        _ => hive.ToString()
    };

    private static int GetImpactRank(StartupImpactLevel impactLevel) => impactLevel switch
    {
        StartupImpactLevel.Review => 4,
        StartupImpactLevel.High => 3,
        StartupImpactLevel.Medium => 2,
        StartupImpactLevel.Low => 1,
        _ => 0
    };
}
