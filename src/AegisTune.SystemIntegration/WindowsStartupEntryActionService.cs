using System.Globalization;
using System.Runtime.Versioning;
using AegisTune.Core;
using Microsoft.Win32;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupEntryActionService : IStartupEntryActionService
{
    private const string DisabledStartupRegistryPath = @"Software\AegisTune\DisabledStartupEntries";
    private const string OriginalRegistryValueKindName = "OriginalRegistryValueKind";
    private readonly IRiskyChangePreflightService _preflightService;
    private readonly IUndoJournalStore _undoJournalStore;

    public WindowsStartupEntryActionService(IRiskyChangePreflightService preflightService, IUndoJournalStore undoJournalStore)
    {
        _preflightService = preflightService;
        _undoJournalStore = undoJournalStore;
    }

    public Task<StartupEntryActionResult> DisableEntryAsync(
        StartupEntryRecord entry,
        CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            ArgumentNullException.ThrowIfNull(entry);

            DateTimeOffset processedAt = DateTimeOffset.Now;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entry.CanDisableFromStartup)
                {
                    return new StartupEntryActionResult(
                        false,
                        "This startup entry does not expose a safe disable path in the current milestone.",
                        processedAt);
                }

                if (entry.Origin == StartupEntryOrigin.RegistryValue)
                {
                    RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
                        new RiskyChangePreflightRequest(
                            RiskyChangeType.StartupRegistryDisable,
                            $"Startup disable for {entry.Name}",
                            SystemRestoreIntent.ModifySettings),
                        dryRunEnabled: false,
                        cancellationToken);

                    if (!preflight.ShouldProceed)
                    {
                        return new StartupEntryActionResult(
                            false,
                            preflight.StatusLine,
                            processedAt,
                            GuidanceLine: preflight.GuidanceLine);
                    }

                    StartupEntryActionResult result = WithPreflightMessage(
                        DisableRegistryValue(entry, processedAt),
                        preflight);
                    await TryAppendStartupJournalEntryAsync(
                        entry.Name,
                        entry.SourceLocationLabel,
                        entry.LaunchCommand,
                        result,
                        UndoJournalEntryKind.StartupDisable,
                        preflight,
                        cancellationToken);
                    return result;
                }

                StartupEntryActionResult fileResult = entry.Origin switch
                {
                    StartupEntryOrigin.StartupFolderFile => DisableStartupFile(entry, processedAt),
                    _ => new StartupEntryActionResult(false, "Unsupported startup entry origin.", processedAt)
                };
                await TryAppendStartupJournalEntryAsync(
                    entry.Name,
                    entry.SourceLocationLabel,
                    entry.LaunchCommand,
                    fileResult,
                    UndoJournalEntryKind.StartupDisable,
                    preflight: null,
                    cancellationToken);
                return fileResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new StartupEntryActionResult(
                    false,
                    $"Startup disable failed: {ex.Message}",
                    processedAt);
            }
        }, cancellationToken);

    public Task<StartupEntryActionResult> RemoveOrphanedEntryAsync(
        StartupEntryRecord entry,
        CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            ArgumentNullException.ThrowIfNull(entry);

            DateTimeOffset processedAt = DateTimeOffset.Now;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entry.CanRemoveSafely)
                {
                    return new StartupEntryActionResult(
                        false,
                        "This startup entry does not expose a safe removal path in the current milestone.",
                        processedAt);
                }

                if (entry.Origin == StartupEntryOrigin.RegistryValue)
                {
                    RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
                        new RiskyChangePreflightRequest(
                            RiskyChangeType.StartupRegistryCleanup,
                            $"Startup cleanup for {entry.Name}",
                            SystemRestoreIntent.ModifySettings),
                        dryRunEnabled: false,
                        cancellationToken);

                    if (!preflight.ShouldProceed)
                    {
                        return new StartupEntryActionResult(
                            false,
                            preflight.StatusLine,
                            processedAt,
                            GuidanceLine: preflight.GuidanceLine);
                    }

                    StartupEntryActionResult result = WithPreflightMessage(
                        RemoveRegistryValue(entry, processedAt),
                        preflight);
                    await TryAppendStartupJournalEntryAsync(
                        entry.Name,
                        entry.SourceLocationLabel,
                        entry.LaunchCommand,
                        result,
                        UndoJournalEntryKind.StartupCleanup,
                        preflight,
                        cancellationToken);
                    return result;
                }

                StartupEntryActionResult fileResult = entry.Origin switch
                {
                    StartupEntryOrigin.StartupFolderFile => RemoveStartupFile(entry, processedAt),
                    _ => new StartupEntryActionResult(false, "Unsupported startup entry origin.", processedAt)
                };
                await TryAppendStartupJournalEntryAsync(
                    entry.Name,
                    entry.SourceLocationLabel,
                    entry.LaunchCommand,
                    fileResult,
                    UndoJournalEntryKind.StartupCleanup,
                    preflight: null,
                    cancellationToken);
                return fileResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new StartupEntryActionResult(
                    false,
                    $"Startup cleanup failed: {ex.Message}",
                    processedAt);
            }
        }, cancellationToken);

    public Task<StartupEntryActionResult> RestoreDisabledEntryAsync(
        UndoJournalEntry entry,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            ArgumentNullException.ThrowIfNull(entry);

            DateTimeOffset processedAt = DateTimeOffset.Now;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entry.CanRunStartupRestore)
                {
                    return new StartupEntryActionResult(
                        false,
                        "This undo entry does not expose a startup restore action.",
                        processedAt);
                }

                string startupEntryName = ExtractStartupEntryName(entry.Title);
                if (!string.IsNullOrWhiteSpace(entry.ArtifactPath))
                {
                    StartupEntryActionResult fileResult = RestoreStartupFile(entry, startupEntryName, processedAt, dryRunEnabled);
                    await TryAppendStartupJournalEntryAsync(
                        startupEntryName,
                        entry.TargetDetail ?? string.Empty,
                        entry.CommandLine ?? string.Empty,
                        fileResult,
                        UndoJournalEntryKind.StartupRestore,
                        preflight: null,
                        cancellationToken);
                    return fileResult;
                }

                StartupRegistryBackupCandidate? backupCandidate = TryResolveRegistryBackupCandidate(entry);
                if (backupCandidate is null)
                {
                    return new StartupEntryActionResult(
                        false,
                        $"AegisTune could not find the saved startup backup metadata for {startupEntryName}.",
                        processedAt,
                        GuidanceLine: "Open the undo journal folder and verify whether the registry backup metadata was removed or already restored.");
                }

                RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
                    new RiskyChangePreflightRequest(
                        RiskyChangeType.StartupRestore,
                        $"Startup restore for {startupEntryName}",
                        SystemRestoreIntent.ModifySettings),
                    dryRunEnabled,
                    cancellationToken);

                if (!preflight.ShouldProceed)
                {
                    return new StartupEntryActionResult(
                        false,
                        preflight.StatusLine,
                        processedAt,
                        WasDryRun: false,
                        GuidanceLine: preflight.GuidanceLine);
                }

                StartupEntryActionResult restoreResult = WithPreflightMessage(
                    RestoreRegistryValue(backupCandidate, startupEntryName, processedAt, dryRunEnabled),
                    preflight);
                await TryAppendStartupJournalEntryAsync(
                    startupEntryName,
                    backupCandidate.OriginalSourceDetail,
                    backupCandidate.OriginalLaunchCommand,
                    restoreResult,
                    UndoJournalEntryKind.StartupRestore,
                    preflight,
                    cancellationToken);
                return restoreResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new StartupEntryActionResult(
                    false,
                    $"Startup restore failed: {ex.Message}",
                    processedAt);
            }
        }, cancellationToken);

    private static StartupEntryActionResult WithPreflightMessage(
        StartupEntryActionResult actionResult,
        RiskyChangePreflightResult preflight) =>
        string.IsNullOrWhiteSpace(preflight.StatusLine)
            ? actionResult
            : actionResult with
            {
                Message = $"{preflight.StatusLine} {actionResult.Message}"
            };

    private static StartupEntryActionResult RemoveRegistryValue(StartupEntryRecord entry, DateTimeOffset processedAt)
    {
        string registryLocation = entry.RegistryLocation
            ?? throw new InvalidOperationException("Registry location was not recorded for this entry.");
        string registryViewName = entry.RegistryViewName
            ?? throw new InvalidOperationException("Registry view was not recorded for this entry.");
        string valueName = entry.RegistryValueName
            ?? throw new InvalidOperationException("Registry value name was not recorded for this entry.");

        ParseRegistryLocation(registryLocation, out RegistryHive hive, out string subKeyPath);
        RegistryView view = ParseRegistryView(registryViewName);

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? writableKey = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (writableKey is null)
        {
            return new StartupEntryActionResult(
                false,
                $"Startup registry key could not be opened for writing: {registryLocation}.",
                processedAt);
        }

        string[] valueNames = writableKey.GetValueNames();
        if (!valueNames.Contains(valueName, StringComparer.Ordinal))
        {
            return new StartupEntryActionResult(
                true,
                $"The stale registry startup entry was already removed from {registryLocation}.",
                processedAt);
        }

        writableKey.DeleteValue(valueName, throwOnMissingValue: false);
        return new StartupEntryActionResult(
            true,
            $"Removed the stale startup registry entry from {registryLocation}.",
            processedAt);
    }

    private static StartupEntryActionResult DisableRegistryValue(StartupEntryRecord entry, DateTimeOffset processedAt)
    {
        string registryLocation = entry.RegistryLocation
            ?? throw new InvalidOperationException("Registry location was not recorded for this entry.");
        string registryViewName = entry.RegistryViewName
            ?? throw new InvalidOperationException("Registry view was not recorded for this entry.");
        string valueName = entry.RegistryValueName
            ?? throw new InvalidOperationException("Registry value name was not recorded for this entry.");

        ParseRegistryLocation(registryLocation, out RegistryHive hive, out string subKeyPath);
        RegistryView view = ParseRegistryView(registryViewName);

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? writableKey = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (writableKey is null)
        {
            return new StartupEntryActionResult(
                false,
                $"Startup registry key could not be opened for writing: {registryLocation}.",
                processedAt);
        }

        string[] valueNames = writableKey.GetValueNames();
        if (!valueNames.Contains(valueName, StringComparer.Ordinal))
        {
            return new StartupEntryActionResult(
                true,
                $"The startup registry entry was already absent from {registryLocation}.",
                processedAt);
        }

        string launchCommand = writableKey.GetValue(valueName)?.ToString()
            ?? entry.LaunchCommand;
        RegistryValueKind valueKind = writableKey.GetValueKind(valueName);
        WriteRegistryBackup(baseKey, entry, launchCommand, valueKind, processedAt);
        writableKey.DeleteValue(valueName, throwOnMissingValue: false);

        return new StartupEntryActionResult(
            true,
            $"Disabled startup launch for {entry.Name} by removing the registry startup value and saving backup metadata.",
            processedAt);
    }

    private static StartupEntryActionResult RemoveStartupFile(StartupEntryRecord entry, DateTimeOffset processedAt)
    {
        string filePath = entry.StartupFilePath
            ?? throw new InvalidOperationException("Startup file path was not recorded for this entry.");

        if (!File.Exists(filePath))
        {
            return new StartupEntryActionResult(
                true,
                $"The stale startup file was already absent: {filePath}.",
                processedAt);
        }

        File.Delete(filePath);
        return new StartupEntryActionResult(
            true,
            $"Removed the stale startup file: {filePath}.",
            processedAt);
    }

    private static StartupEntryActionResult DisableStartupFile(StartupEntryRecord entry, DateTimeOffset processedAt)
    {
        string filePath = entry.StartupFilePath
            ?? throw new InvalidOperationException("Startup file path was not recorded for this entry.");

        if (!File.Exists(filePath))
        {
            return new StartupEntryActionResult(
                true,
                $"The startup file was already absent: {filePath}.",
                processedAt);
        }

        string destinationRoot = GetDisabledStartupBackupRoot(entry.ScopeLabel);
        Directory.CreateDirectory(destinationRoot);

        string extension = Path.GetExtension(filePath);
        string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(filePath));
        string destinationFileName = $"{baseName}-{processedAt:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        string destinationPath = Path.Combine(destinationRoot, destinationFileName);

        File.Move(filePath, destinationPath);
        File.WriteAllText(
            Path.Combine(destinationRoot, $"{destinationFileName}.origin.txt"),
            string.Join(
                Environment.NewLine,
                new[]
                {
                    $"Original path: {filePath}",
                    $"Disabled at: {processedAt:O}",
                    $"Entry name: {entry.Name}",
                    $"Launch command: {entry.LaunchCommand}"
                }));

        return new StartupEntryActionResult(
            true,
            $"Disabled startup launch for {entry.Name} by moving the startup file out of the startup folder.",
            processedAt,
            destinationPath);
    }

    private static StartupEntryActionResult RestoreStartupFile(
        UndoJournalEntry entry,
        string startupEntryName,
        DateTimeOffset processedAt,
        bool dryRunEnabled)
    {
        string backupFilePath = entry.ArtifactPath
            ?? throw new InvalidOperationException("The moved startup file path was not recorded.");

        if (!File.Exists(backupFilePath))
        {
            return new StartupEntryActionResult(
                false,
                $"The moved startup file is no longer available: {backupFilePath}.",
                processedAt,
                GuidanceLine: "The startup entry may already be restored or the saved file was deleted.");
        }

        string originMetadataPath = $"{backupFilePath}.origin.txt";
        if (!File.Exists(originMetadataPath))
        {
            return new StartupEntryActionResult(
                false,
                $"The startup restore metadata file is missing: {originMetadataPath}.",
                processedAt,
                GuidanceLine: "AegisTune needs the saved origin file to move the startup entry back into its original startup folder.");
        }

        if (!TryReadOriginalStartupPath(originMetadataPath, out string originalPath))
        {
            return new StartupEntryActionResult(
                false,
                "The startup restore metadata could not be parsed.",
                processedAt,
                GuidanceLine: "Open the .origin.txt file and verify that it still contains the original startup file path.");
        }

        if (dryRunEnabled)
        {
            return new StartupEntryActionResult(
                true,
                $"Dry-run mode is active. AegisTune previewed restoring {startupEntryName} to {originalPath}.",
                processedAt,
                originalPath,
                WasDryRun: true,
                GuidanceLine: "Disable dry-run in Settings when you are ready to move the startup file back into the startup folder.");
        }

        if (File.Exists(originalPath))
        {
            return new StartupEntryActionResult(
                false,
                $"A startup file already exists at the original path: {originalPath}.",
                processedAt,
                originalPath,
                GuidanceLine: "Review the existing startup file before you overwrite or remove it.");
        }

        string? originalDirectory = Path.GetDirectoryName(originalPath);
        if (string.IsNullOrWhiteSpace(originalDirectory))
        {
            return new StartupEntryActionResult(
                false,
                $"The original startup folder could not be resolved for {originalPath}.",
                processedAt);
        }

        Directory.CreateDirectory(originalDirectory);
        File.Move(backupFilePath, originalPath);
        File.Delete(originMetadataPath);

        return new StartupEntryActionResult(
            true,
            $"Restored startup launch for {startupEntryName} by moving the saved startup file back into its original startup folder.",
            processedAt,
            originalPath,
            GuidanceLine: "Re-scan Startup Review to confirm the app is active again and no duplicate launch entry was created.");
    }

    private static StartupEntryActionResult RestoreRegistryValue(
        StartupRegistryBackupCandidate backupCandidate,
        string startupEntryName,
        DateTimeOffset processedAt,
        bool dryRunEnabled)
    {
        if (dryRunEnabled)
        {
            return new StartupEntryActionResult(
                true,
                $"Dry-run mode is active. AegisTune previewed restoring the startup registry value for {startupEntryName}.",
                processedAt,
                WasDryRun: true,
                GuidanceLine: "Disable dry-run in Settings when you are ready to write the saved startup value back into the registry.");
        }

        ParseRegistryLocation(backupCandidate.OriginalRegistryLocation, out RegistryHive hive, out string subKeyPath);
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, backupCandidate.OriginalRegistryView);
        using RegistryKey writableKey = baseKey.CreateSubKey(subKeyPath)
            ?? throw new InvalidOperationException($"The original startup registry path could not be recreated: {backupCandidate.OriginalRegistryLocation}");

        object? existingValue = writableKey.GetValue(backupCandidate.OriginalRegistryValueName);
        if (!string.Equals(existingValue?.ToString(), backupCandidate.OriginalLaunchCommand, StringComparison.Ordinal))
        {
            writableKey.SetValue(
                backupCandidate.OriginalRegistryValueName,
                backupCandidate.OriginalLaunchCommand,
                backupCandidate.OriginalRegistryValueKind);
        }

        using RegistryKey backupStorageBaseKey = RegistryKey.OpenBaseKey(backupCandidate.BackupHive, backupCandidate.BackupStorageView);
        backupStorageBaseKey.DeleteSubKeyTree($@"{DisabledStartupRegistryPath}\{backupCandidate.BackupEntryName}", throwOnMissingSubKey: false);

        return new StartupEntryActionResult(
            true,
            $"Restored startup launch for {startupEntryName} by writing the saved startup registry value back to {backupCandidate.OriginalRegistryLocation}.",
            processedAt,
            GuidanceLine: "Re-scan Startup Review to confirm the restored entry is active again and that no duplicate launch values were created.");
    }

    private static void ParseRegistryLocation(string registryLocation, out RegistryHive hive, out string subKeyPath)
    {
        const string currentUserPrefix = @"HKEY_CURRENT_USER\";
        const string localMachinePrefix = @"HKEY_LOCAL_MACHINE\";

        if (registryLocation.StartsWith(currentUserPrefix, StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            subKeyPath = registryLocation[currentUserPrefix.Length..];
            return;
        }

        if (registryLocation.StartsWith(localMachinePrefix, StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            subKeyPath = registryLocation[localMachinePrefix.Length..];
            return;
        }

        throw new InvalidOperationException($"Unsupported registry location: {registryLocation}");
    }

    private static RegistryView ParseRegistryView(string registryViewName) => registryViewName switch
    {
        nameof(RegistryView.Registry64) => RegistryView.Registry64,
        nameof(RegistryView.Registry32) => RegistryView.Registry32,
        _ => RegistryView.Default
    };

    private static void WriteRegistryBackup(
        RegistryKey baseKey,
        StartupEntryRecord entry,
        string launchCommand,
        RegistryValueKind valueKind,
        DateTimeOffset processedAt)
    {
        using RegistryKey backupRoot = baseKey.CreateSubKey(DisabledStartupRegistryPath)
            ?? throw new InvalidOperationException("Startup backup key could not be created.");
        using RegistryKey backupEntry = backupRoot.CreateSubKey($"{processedAt:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}")
            ?? throw new InvalidOperationException("Startup backup entry could not be created.");

        backupEntry.SetValue("EntryName", entry.Name);
        backupEntry.SetValue("OriginalRegistryLocation", entry.RegistryLocation ?? string.Empty);
        backupEntry.SetValue("OriginalRegistryValueName", entry.RegistryValueName ?? string.Empty);
        backupEntry.SetValue("OriginalRegistryView", entry.RegistryViewName ?? string.Empty);
        backupEntry.SetValue(OriginalRegistryValueKindName, valueKind.ToString());
        backupEntry.SetValue("OriginalSource", entry.Source);
        backupEntry.SetValue("OriginalScope", entry.ScopeLabel);
        backupEntry.SetValue("OriginalLaunchCommand", launchCommand);
        backupEntry.SetValue("DisabledAtUtc", processedAt.UtcDateTime.ToString("O"));
    }

    private static string GetDisabledStartupBackupRoot(string scopeLabel)
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string scopeSegment = scopeLabel.Contains("All users", StringComparison.OrdinalIgnoreCase)
            ? "AllUsers"
            : "CurrentUser";

        return Path.Combine(basePath, "AegisTune", "DisabledStartupEntries", scopeSegment);
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => Array.IndexOf(invalid, character) < 0).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? "startup-entry"
            : sanitized;
    }

    private static bool TryReadOriginalStartupPath(string metadataPath, out string originalPath)
    {
        const string prefix = "Original path: ";

        foreach (string line in File.ReadLines(metadataPath))
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            originalPath = line[prefix.Length..].Trim();
            return !string.IsNullOrWhiteSpace(originalPath);
        }

        originalPath = string.Empty;
        return false;
    }

    private static string ExtractStartupEntryName(string title)
    {
        int separatorIndex = title.IndexOf(':');
        return separatorIndex >= 0 && separatorIndex < title.Length - 1
            ? title[(separatorIndex + 1)..].Trim()
            : title;
    }

    private static StartupRegistryBackupCandidate? TryResolveRegistryBackupCandidate(UndoJournalEntry entry)
    {
        if (!TryParseStartupSourceDetail(entry.TargetDetail, out string originalRegistryLocation, out string originalRegistryValueName))
        {
            return null;
        }

        ParseRegistryLocation(originalRegistryLocation, out RegistryHive backupHive, out _);

        StartupRegistryBackupCandidate? bestCandidate = null;
        foreach (RegistryView backupView in GetRegistryViews())
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(backupHive, backupView);
            using RegistryKey? backupRoot = baseKey.OpenSubKey(DisabledStartupRegistryPath);
            if (backupRoot is null)
            {
                continue;
            }

            foreach (string backupEntryName in backupRoot.GetSubKeyNames())
            {
                using RegistryKey? backupEntry = backupRoot.OpenSubKey(backupEntryName);
                if (backupEntry is null)
                {
                    continue;
                }

                string? candidateLocation = backupEntry.GetValue("OriginalRegistryLocation")?.ToString();
                string candidateValueName = backupEntry.GetValue("OriginalRegistryValueName")?.ToString() ?? string.Empty;
                string candidateLaunchCommand = backupEntry.GetValue("OriginalLaunchCommand")?.ToString() ?? string.Empty;

                if (!string.Equals(candidateLocation, originalRegistryLocation, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(candidateValueName, originalRegistryValueName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.CommandLine)
                    && !string.Equals(candidateLaunchCommand, entry.CommandLine, StringComparison.Ordinal))
                {
                    continue;
                }

                string originalRegistryViewName = backupEntry.GetValue("OriginalRegistryView")?.ToString() ?? nameof(RegistryView.Default);
                string originalSourceDetail = $"{originalRegistryLocation} [{(string.IsNullOrEmpty(candidateValueName) ? "(Default)" : candidateValueName)}]";
                DateTimeOffset disabledAt = TryParseDisabledAt(backupEntry.GetValue("DisabledAtUtc")?.ToString(), out DateTimeOffset parsedDisabledAt)
                    ? parsedDisabledAt
                    : DateTimeOffset.MinValue;

                StartupRegistryBackupCandidate candidate = new(
                    backupHive,
                    backupView,
                    backupEntryName,
                    originalRegistryLocation,
                    candidateValueName,
                    ParseRegistryView(originalRegistryViewName),
                    ParseRegistryValueKind(backupEntry.GetValue(OriginalRegistryValueKindName)?.ToString()),
                    candidateLaunchCommand,
                    originalSourceDetail,
                    disabledAt);

                if (bestCandidate is null || candidate.DisabledAtUtc > bestCandidate.DisabledAtUtc)
                {
                    bestCandidate = candidate;
                }
            }
        }

        return bestCandidate;
    }

    private static bool TryParseStartupSourceDetail(string? sourceDetail, out string registryLocation, out string registryValueName)
    {
        registryLocation = string.Empty;
        registryValueName = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceDetail))
        {
            return false;
        }

        int bracketIndex = sourceDetail.LastIndexOf(" [", StringComparison.Ordinal);
        if (bracketIndex <= 0 || !sourceDetail.EndsWith(']'))
        {
            return false;
        }

        registryLocation = sourceDetail[..bracketIndex].Trim();
        registryValueName = sourceDetail[(bracketIndex + 2)..^1];
        if (string.Equals(registryValueName, "(Default)", StringComparison.Ordinal))
        {
            registryValueName = string.Empty;
        }

        return !string.IsNullOrWhiteSpace(registryLocation);
    }

    private static bool TryParseDisabledAt(string? rawValue, out DateTimeOffset disabledAtUtc) =>
        DateTimeOffset.TryParse(
            rawValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out disabledAtUtc);

    private static RegistryValueKind ParseRegistryValueKind(string? rawValue) =>
        Enum.TryParse(rawValue, ignoreCase: true, out RegistryValueKind valueKind)
            ? valueKind
            : RegistryValueKind.String;

    private static IEnumerable<RegistryView> GetRegistryViews()
    {
        if (Environment.Is64BitOperatingSystem)
        {
            return [RegistryView.Registry64, RegistryView.Registry32];
        }

        return [RegistryView.Default];
    }

    private async Task TryAppendStartupJournalEntryAsync(
        string entryName,
        string sourceLocation,
        string launchCommand,
        StartupEntryActionResult result,
        UndoJournalEntryKind journalKind,
        RiskyChangePreflightResult? preflight,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded || result.WasDryRun)
        {
            return;
        }

        try
        {
            await _undoJournalStore.AppendAsync(
                new UndoJournalEntry(
                    Guid.NewGuid(),
                    journalKind,
                    $"{GetStartupJournalTitlePrefix(journalKind)}: {entryName}",
                    result.ProcessedAt,
                    result.Message,
                    result.GuidanceLine ?? GetStartupJournalGuidance(journalKind),
                    RestorePointCreated: preflight?.RestorePointCreated ?? false,
                    RestorePointReused: preflight?.RestorePointReused ?? false,
                    ArtifactPath: result.ArtifactPath,
                    TargetDetail: sourceLocation,
                    CommandLine: launchCommand),
                cancellationToken);
        }
        catch
        {
            // Journal persistence must not block the startup action result.
        }
    }

    private static string GetStartupJournalTitlePrefix(UndoJournalEntryKind journalKind) => journalKind switch
    {
        UndoJournalEntryKind.StartupDisable => "Startup disable",
        UndoJournalEntryKind.StartupCleanup => "Startup cleanup",
        UndoJournalEntryKind.StartupRestore => "Startup restore",
        _ => "Startup action"
    };

    private static string GetStartupJournalGuidance(UndoJournalEntryKind journalKind) => journalKind switch
    {
        UndoJournalEntryKind.StartupDisable =>
            "Review Startup Review and Safety & Undo before restoring or changing this launch path again.",
        UndoJournalEntryKind.StartupCleanup =>
            "Re-scan Startup Review to confirm the stale launch entry is gone and no replacement entry was created.",
        UndoJournalEntryKind.StartupRestore =>
            "Re-scan Startup Review to confirm the startup entry is active again and that the restored launch path still resolves cleanly.",
        _ => "Review the latest startup action before making another launch-state change."
    };

    private sealed record StartupRegistryBackupCandidate(
        RegistryHive BackupHive,
        RegistryView BackupStorageView,
        string BackupEntryName,
        string OriginalRegistryLocation,
        string OriginalRegistryValueName,
        RegistryView OriginalRegistryView,
        RegistryValueKind OriginalRegistryValueKind,
        string OriginalLaunchCommand,
        string OriginalSourceDetail,
        DateTimeOffset DisabledAtUtc);
}
