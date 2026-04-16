using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryRollbackService : IRegistryRollbackService
{
    private readonly IRiskyChangePreflightService _preflightService;
    private readonly IUndoJournalStore _undoJournalStore;

    public WindowsRegistryRollbackService(
        IRiskyChangePreflightService preflightService,
        IUndoJournalStore undoJournalStore)
    {
        _preflightService = preflightService;
        _undoJournalStore = undoJournalStore;
    }

    public async Task<RegistryRollbackExecutionResult> RollbackAsync(
        UndoJournalEntry entry,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        DateTimeOffset processedAt = DateTimeOffset.Now;

        if (!entry.CanRunRegistryRollback || !entry.HasRegistryBackup)
        {
            return new RegistryRollbackExecutionResult(
                false,
                dryRunEnabled,
                "This undo entry does not expose a runnable registry rollback file.",
                "Pick a registry repair entry that includes a .reg backup file.",
                processedAt);
        }

        string backupFilePath = entry.RegistryBackupPath!;
        if (!File.Exists(backupFilePath))
        {
            return new RegistryRollbackExecutionResult(
                false,
                dryRunEnabled,
                $"The registry backup file is no longer available: {backupFilePath}",
                "Open the undo journal folder and verify whether the backup file was moved or deleted.",
                processedAt);
        }

        RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
            new RiskyChangePreflightRequest(
                RiskyChangeType.RegistryRepair,
                $"Registry rollback for {entry.Title}",
                SystemRestoreIntent.ModifySettings),
            dryRunEnabled,
            cancellationToken);

        if (!preflight.ShouldProceed)
        {
            return new RegistryRollbackExecutionResult(
                false,
                false,
                preflight.StatusLine,
                preflight.GuidanceLine,
                processedAt);
        }

        if (dryRunEnabled)
        {
            return new RegistryRollbackExecutionResult(
                true,
                true,
                $"Dry-run mode is active. AegisTune previewed registry rollback from {backupFilePath}.",
                "Disable dry-run in Settings when you are ready to import the backup into the registry.",
                processedAt);
        }

        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"import \"{backupFilePath}\"",
                UseShellExecute = true,
                Verb = "runas"
            }) ?? throw new InvalidOperationException("Failed to start reg.exe for rollback.");

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return new RegistryRollbackExecutionResult(
                    false,
                    false,
                    $"{preflight.StatusLine} Registry rollback exited with code {process.ExitCode}.",
                    "Review the backup file and elevation context before retrying the rollback.",
                    processedAt);
            }

            RegistryRollbackExecutionResult result = new(
                true,
                false,
                $"{preflight.StatusLine} Imported the registry backup for {entry.Title}.",
                "Re-scan Repair and Windows Health to confirm the previous issue is gone and the restored key now behaves as expected.",
                processedAt);

            await _undoJournalStore.AppendAsync(
                new UndoJournalEntry(
                    Guid.NewGuid(),
                    UndoJournalEntryKind.RegistryRollback,
                    entry.Title,
                    processedAt,
                    result.StatusLine,
                    result.GuidanceLine,
                    RestorePointCreated: preflight.RestorePointCreated,
                    RestorePointReused: preflight.RestorePointReused,
                    RegistryBackupPath: backupFilePath,
                    RegistryTargetPath: entry.RegistryTargetPath),
                cancellationToken);

            return result;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new RegistryRollbackExecutionResult(
                false,
                false,
                $"{preflight.StatusLine} The elevation prompt was canceled before the registry backup could be imported.",
                "Run the rollback again when you are ready to approve the elevated registry import.",
                processedAt);
        }
    }
}
