using System.Runtime.Versioning;
using AegisTune.Core;
using Microsoft.Win32;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryRepairExecutionService : IRegistryRepairExecutionService
{
    private readonly IRegistryBackupService _backupService;
    private readonly IRiskyChangePreflightService _preflightService;
    private readonly IUndoJournalStore _undoJournalStore;

    public WindowsRegistryRepairExecutionService(
        IRegistryBackupService backupService,
        IRiskyChangePreflightService preflightService,
        IUndoJournalStore undoJournalStore)
    {
        _backupService = backupService;
        _preflightService = preflightService;
        _undoJournalStore = undoJournalStore;
    }

    public async Task<RegistryRepairExecutionResult> ExecuteAsync(
        RepairCandidateRecord candidate,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        DateTimeOffset processedAt = DateTimeOffset.Now;

        if (!candidate.CanExecuteInAppRepairPack)
        {
            return new RegistryRepairExecutionResult(
                false,
                dryRunEnabled,
                "This repair candidate does not expose an in-app registry repair pack.",
                "Use the linked official repair route or Windows surface for this issue.",
                processedAt);
        }

        RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
            new RiskyChangePreflightRequest(
                RiskyChangeType.RegistryRepair,
                candidate.Title,
                SystemRestoreIntent.ModifySettings),
            dryRunEnabled,
            cancellationToken);

        if (!preflight.ShouldProceed)
        {
            return new RegistryRepairExecutionResult(
                false,
                false,
                preflight.StatusLine,
                preflight.GuidanceLine,
                processedAt);
        }

        if (dryRunEnabled)
        {
            return new RegistryRepairExecutionResult(
                true,
                true,
                $"Dry-run mode is active. AegisTune previewed {candidate.RepairActionLabelText.ToLowerInvariant()} for {candidate.RegistryPathLabel}.",
                preflight.GuidanceLine,
                processedAt);
        }

        RegistryBackupResult backupResult = await _backupService.BackupKeyAsync(candidate.RegistryPath!, cancellationToken);
        if (!backupResult.Succeeded)
        {
            return new RegistryRepairExecutionResult(
                false,
                false,
                backupResult.StatusLine,
                "AegisTune blocked the registry repair because the backup file could not be created first.",
                processedAt);
        }

        try
        {
            switch (candidate.RegistryRepairPackKind)
            {
                case RegistryRepairPackKind.RemoveRegistryKey:
                    DeleteRegistryKey(candidate.RegistryPath!);
                    RegistryRepairExecutionResult removeResult = new(
                        true,
                        false,
                        $"{preflight.StatusLine} Removed the stale registry key for {candidate.Title}.",
                        $"A .reg backup was written to {backupResult.BackupFilePath}. Re-scan the Repair page to confirm the issue is gone.",
                        processedAt,
                        backupResult.BackupFilePath);
                    await AppendUndoEntryAsync(candidate, removeResult, preflight, cancellationToken);
                    return removeResult;
                case RegistryRepairPackKind.SetDwordValue:
                    SetRegistryDword(candidate.RegistryPath!, candidate.RegistryValueName, candidate.RegistryDwordValue);
                    RegistryRepairExecutionResult dwordResult = new(
                        true,
                        false,
                        $"{preflight.StatusLine} Applied the registry repair pack for {candidate.Title}.",
                        $"A .reg backup was written to {backupResult.BackupFilePath}. Re-scan Health and Repair to verify the Windows service no longer needs review.",
                        processedAt,
                        backupResult.BackupFilePath);
                    await AppendUndoEntryAsync(candidate, dwordResult, preflight, cancellationToken);
                    return dwordResult;
                default:
                    return new RegistryRepairExecutionResult(
                        false,
                        false,
                        "Unsupported registry repair pack kind.",
                        "This repair candidate needs a supported registry action before it can run in-app.",
                        processedAt);
            }
        }
        catch (Exception ex)
        {
            return new RegistryRepairExecutionResult(
                false,
                false,
                $"Registry repair failed for {candidate.Title}: {ex.Message}",
                $"A .reg backup was written to {backupResult.BackupFilePath}. Use it for rollback if needed.",
                processedAt,
                backupResult.BackupFilePath);
        }
    }

    private Task AppendUndoEntryAsync(
        RepairCandidateRecord candidate,
        RegistryRepairExecutionResult result,
        RiskyChangePreflightResult preflight,
        CancellationToken cancellationToken) =>
        _undoJournalStore.AppendAsync(
            new UndoJournalEntry(
                Guid.NewGuid(),
                UndoJournalEntryKind.RegistryRepair,
                candidate.Title,
                result.ProcessedAt,
                result.StatusLine,
                result.GuidanceLine,
                RestorePointCreated: preflight.RestorePointCreated,
                RestorePointReused: preflight.RestorePointReused,
                RegistryBackupPath: result.BackupFilePath,
                RegistryTargetPath: candidate.RegistryPath),
            cancellationToken);

    private static void DeleteRegistryKey(string registryPath)
    {
        RegistryPathUtility.ParseRegistryPath(registryPath, out RegistryHive hive, out string subKeyPath);
        RegistryView view = hive == RegistryHive.CurrentUser
            ? RegistryView.Default
            : Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64
                : RegistryView.Default;

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? key = baseKey.OpenSubKey(subKeyPath);
        if (key is null)
        {
            return;
        }

        baseKey.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
    }

    private static void SetRegistryDword(string registryPath, string? valueName, int? value)
    {
        if (string.IsNullOrWhiteSpace(valueName) || value is null)
        {
            throw new InvalidOperationException("The registry repair pack did not specify a DWORD target.");
        }

        RegistryPathUtility.ParseRegistryPath(registryPath, out RegistryHive hive, out string subKeyPath);
        RegistryView view = hive == RegistryHive.CurrentUser
            ? RegistryView.Default
            : Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64
                : RegistryView.Default;

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? writableKey = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (writableKey is null)
        {
            throw new InvalidOperationException($"Registry key could not be opened for writing: {registryPath}");
        }

        writableKey.SetValue(valueName, value.Value, RegistryValueKind.DWord);
    }
}
