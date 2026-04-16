using System.Runtime.Versioning;
using System.Text;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsApplicationResidueCleanupService : IApplicationResidueCleanupService
{
    private readonly ISettingsStore _settingsStore;
    private readonly IUndoJournalStore _undoJournalStore;

    public WindowsApplicationResidueCleanupService(
        ISettingsStore settingsStore,
        IUndoJournalStore undoJournalStore)
    {
        _settingsStore = settingsStore;
        _undoJournalStore = undoJournalStore;
    }

    public async Task<ApplicationResidueCleanupExecutionResult> CleanupAsync(
        InstalledApplicationRecord application,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        DateTimeOffset processedAt = DateTimeOffset.Now;

        if (!application.CanCleanConfirmedResidue)
        {
            return new ApplicationResidueCleanupExecutionResult(
                application.DisplayName,
                dryRunEnabled,
                false,
                0,
                0,
                processedAt,
                "This app does not expose confirmed leftover folders in the current scan.",
                "Refresh Apps & Uninstall first, then run leftover cleanup only when residue folders are still listed.");
        }

        AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);
        ApplicationResidueRecord[] confirmedResidue = application.FilesystemResidue
            .Where(entry => Directory.Exists(entry.Path))
            .Where(entry => !IsExcluded(entry.Path, settings.CleanupExclusions))
            .ToArray();
        ApplicationResidueRecord[] excludedResidue = application.FilesystemResidue
            .Where(entry => Directory.Exists(entry.Path))
            .Where(entry => IsExcluded(entry.Path, settings.CleanupExclusions))
            .ToArray();

        if (confirmedResidue.Length == 0)
        {
            string guidance = excludedResidue.Length > 0
                ? "The current residue folders are excluded by your cleanup exclusion rules in Settings."
                : "Refresh Apps & Uninstall to confirm whether the leftover folders are still present.";

            return new ApplicationResidueCleanupExecutionResult(
                application.DisplayName,
                dryRunEnabled,
                true,
                0,
                0,
                processedAt,
                excludedResidue.Length > 0
                    ? "All confirmed residue folders are currently excluded from cleanup."
                    : "No confirmed residue folders are currently available for cleanup.",
                guidance);
        }

        long totalBytes = confirmedResidue.Sum(entry => entry.SizeBytes);
        if (dryRunEnabled)
        {
            return new ApplicationResidueCleanupExecutionResult(
                application.DisplayName,
                true,
                true,
                confirmedResidue.Length,
                totalBytes,
                processedAt,
                $"Dry-run mode is active. AegisTune previewed moving {confirmedResidue.Length:N0} confirmed leftover folder(s) for {application.DisplayName}.",
                "Disable dry-run in Settings when you are ready to move the listed leftover folders into AegisTune quarantine.");
        }

        string quarantineRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AegisTune",
            "ResidueQuarantine",
            SanitizePathSegment(application.DisplayName),
            $"{processedAt:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(quarantineRoot);

        List<(string OriginalPath, string QuarantinePath, string ScopeLabel, long SizeBytes)> movedResidue = [];
        List<string> failedResidue = [];

        foreach (ApplicationResidueRecord residue in confirmedResidue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(residue.Path))
            {
                continue;
            }

            string destinationPath = Path.Combine(
                quarantineRoot,
                $"{movedResidue.Count + failedResidue.Count + 1:D2}-{SanitizePathSegment(Path.GetFileName(residue.Path))}");

            try
            {
                Directory.Move(residue.Path, destinationPath);
                movedResidue.Add((residue.Path, destinationPath, residue.ScopeLabel, residue.SizeBytes));
            }
            catch (Exception ex)
            {
                failedResidue.Add($"{residue.Path} ({ex.Message})");
            }
        }

        WriteManifestFile(quarantineRoot, application, movedResidue, failedResidue, excludedResidue, processedAt);

        if (movedResidue.Count == 0)
        {
            return new ApplicationResidueCleanupExecutionResult(
                application.DisplayName,
                false,
                false,
                0,
                0,
                processedAt,
                $"AegisTune could not move any confirmed leftover folders for {application.DisplayName}.",
                failedResidue.Count > 0
                    ? "Open the leftover folder or app summary and review the locked or protected paths before retrying cleanup."
                    :
                    "Refresh Apps & Uninstall and confirm the residue folders still exist before retrying cleanup.",
                quarantineRoot);
        }

        ApplicationResidueCleanupExecutionResult result = new(
            application.DisplayName,
            false,
            true,
            movedResidue.Count,
            movedResidue.Sum(entry => entry.SizeBytes),
            processedAt,
            $"Moved {movedResidue.Count:N0} confirmed leftover folder(s) for {application.DisplayName} into AegisTune quarantine.",
            failedResidue.Count > 0
                ? $"Open the quarantine folder to review what moved successfully. {failedResidue.Count:N0} residue path(s) could not be moved."
                : "Refresh Apps & Uninstall to confirm the leftover footprint is gone. Open the quarantine folder if you need to inspect the moved files.",
            quarantineRoot);

        await _undoJournalStore.AppendAsync(
            new UndoJournalEntry(
                Guid.NewGuid(),
                UndoJournalEntryKind.ApplicationResidueCleanup,
                $"Leftover cleanup: {application.DisplayName}",
                processedAt,
                result.StatusLine,
                result.GuidanceLine,
                ArtifactPath: quarantineRoot,
                TargetDetail: $"{movedResidue.Count:N0} confirmed leftover folder(s)",
                CommandLine: string.Join(Environment.NewLine, movedResidue.Select(entry => entry.OriginalPath))),
            cancellationToken);

        return result;
    }

    private static void WriteManifestFile(
        string quarantineRoot,
        InstalledApplicationRecord application,
        IReadOnlyList<(string OriginalPath, string QuarantinePath, string ScopeLabel, long SizeBytes)> movedResidue,
        IReadOnlyList<string> failedResidue,
        IReadOnlyList<ApplicationResidueRecord> excludedResidue,
        DateTimeOffset processedAt)
    {
        StringBuilder builder = new();
        builder.AppendLine($"App: {application.DisplayName}");
        builder.AppendLine($"Processed at: {processedAt:O}");
        builder.AppendLine($"Registry key: {application.RegistryKeyPath}");
        builder.AppendLine($"Uninstall command: {application.UninstallCommandLabel}");
        builder.AppendLine();
        builder.AppendLine("Moved residue:");

        foreach ((string originalPath, string quarantinePath, string scopeLabel, long sizeBytes) in movedResidue)
        {
            builder.AppendLine($"- {scopeLabel}: {originalPath} -> {quarantinePath} ({DataSizeFormatter.FormatBytes(sizeBytes)})");
        }

        if (excludedResidue.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Excluded residue:");
            foreach (ApplicationResidueRecord residue in excludedResidue)
            {
                builder.AppendLine($"- {residue.ScopeLabel}: {residue.Path}");
            }
        }

        if (failedResidue.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Failed residue:");
            foreach (string failure in failedResidue)
            {
                builder.AppendLine($"- {failure}");
            }
        }

        File.WriteAllText(Path.Combine(quarantineRoot, "cleanup-manifest.txt"), builder.ToString());
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => Array.IndexOf(invalidCharacters, character) < 0).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? "app-residue"
            : sanitized;
    }

    private static bool IsExcluded(string path, IReadOnlyList<string> exclusionPatterns)
    {
        string normalizedPath = path.Replace('/', '\\');

        foreach (string pattern in exclusionPatterns)
        {
            string normalizedPattern = pattern.Replace('/', '\\');
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                continue;
            }

            if (Path.IsPathRooted(normalizedPattern))
            {
                if (normalizedPath.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (normalizedPath.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
