using System.ComponentModel;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class PnpUtilDriverInstallService : IDriverInstallService
{
    private readonly IDriverCommandRunner _commandRunner;
    private readonly IRiskyChangePreflightService _preflightService;
    private readonly IUndoJournalStore _undoJournalStore;

    public PnpUtilDriverInstallService(IDriverCommandRunner commandRunner)
        : this(commandRunner, new AllowAllRiskyChangePreflightService(), new NoOpUndoJournalStore())
    {
    }

    public PnpUtilDriverInstallService(
        IDriverCommandRunner commandRunner,
        IRiskyChangePreflightService preflightService)
        : this(commandRunner, preflightService, new NoOpUndoJournalStore())
    {
    }

    public PnpUtilDriverInstallService(
        IDriverCommandRunner commandRunner,
        IRiskyChangePreflightService preflightService,
        IUndoJournalStore undoJournalStore)
    {
        _commandRunner = commandRunner;
        _preflightService = preflightService;
        _undoJournalStore = undoJournalStore;
    }

    public async Task<DriverInstallExecutionResult> InstallAsync(
        DriverDeviceRecord device,
        DriverRepositoryCandidate candidate,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(candidate);

        string infPath = Path.GetFullPath(candidate.InfPath);
        string arguments = BuildArguments(infPath);
        string commandLine = $"pnputil.exe {arguments}";
        DateTimeOffset executedAt = DateTimeOffset.Now;

        if (!File.Exists(infPath))
        {
            return new DriverInstallExecutionResult(
                infPath,
                commandLine,
                dryRunEnabled,
                false,
                null,
                executedAt,
                "The selected INF file is no longer available.",
                "Re-scan the local driver repositories before attempting another install.");
        }

        RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
            new RiskyChangePreflightRequest(
                RiskyChangeType.DriverInstall,
                $"Driver install for {device.FriendlyName}",
                SystemRestoreIntent.DeviceDriverInstall),
            dryRunEnabled,
            cancellationToken);

        if (!preflight.ShouldProceed)
        {
            return new DriverInstallExecutionResult(
                infPath,
                commandLine,
                false,
                false,
                null,
                executedAt,
                preflight.StatusLine,
                preflight.GuidanceLine);
        }

        if (dryRunEnabled)
        {
            return new DriverInstallExecutionResult(
                infPath,
                commandLine,
                true,
                true,
                null,
                executedAt,
                "Dry-run mode is enabled. AegisTune did not call pnputil for this driver candidate.",
                $"{preflight.GuidanceLine} Disable dry-run in Settings when you are ready to elevate and install the selected INF package.");
        }

        try
        {
            int exitCode = await _commandRunner.RunElevatedAsync("pnputil.exe", arguments, cancellationToken);
            bool succeeded = exitCode == 0;
            DriverInstallExecutionResult result = new(
                infPath,
                commandLine,
                false,
                succeeded,
                exitCode,
                executedAt,
                succeeded
                    ? $"{preflight.StatusLine} PnPUtil completed for {Path.GetFileName(infPath)}."
                    : $"{preflight.StatusLine} PnPUtil exited with code {exitCode} for {Path.GetFileName(infPath)}.",
                succeeded
                    ? $"{preflight.GuidanceLine} Re-audit {device.FriendlyName} and confirm provider, version, INF, and device status before closing the ticket."
                    : $"{preflight.GuidanceLine} Review the INF, identifier evidence, and elevation context before attempting another install.");

            if (succeeded)
            {
                await TryAppendJournalEntryAsync(
                    new UndoJournalEntry(
                        Guid.NewGuid(),
                        UndoJournalEntryKind.DriverInstall,
                        $"Driver install: {device.FriendlyName}",
                        executedAt,
                        result.StatusLine,
                        result.VerificationHint,
                        RestorePointCreated: preflight.RestorePointCreated,
                        RestorePointReused: preflight.RestorePointReused,
                        ArtifactPath: infPath,
                        TargetDetail: device.FriendlyName,
                        CommandLine: commandLine),
                    cancellationToken);
            }

            return result;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new DriverInstallExecutionResult(
                infPath,
                commandLine,
                false,
                false,
                null,
                executedAt,
                $"{preflight.StatusLine} The elevation prompt was canceled before pnputil could run.",
                $"{preflight.GuidanceLine} Re-run the install when you are ready to approve the elevated driver operation.");
        }
    }

    public static string BuildArguments(string infPath) =>
        $"/add-driver \"{infPath}\" /install";

    private sealed class AllowAllRiskyChangePreflightService : IRiskyChangePreflightService
    {
        public Task<RiskyChangePreflightResult> PrepareAsync(
            RiskyChangePreflightRequest request,
            bool dryRunEnabled,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RiskyChangePreflightResult(
                true,
                false,
                false,
                dryRunEnabled,
                DateTimeOffset.Now,
                "Restore-point preflight is not active for this driver install instance.",
                "This driver install path is running without the shared restore-point preflight service."));
    }

    private async Task TryAppendJournalEntryAsync(UndoJournalEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await _undoJournalStore.AppendAsync(entry, cancellationToken);
        }
        catch
        {
            // Journal persistence must not block the driver install result.
        }
    }

    private sealed class NoOpUndoJournalStore : IUndoJournalStore
    {
        public string StoragePath => string.Empty;

        public Task<IReadOnlyList<UndoJournalEntry>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UndoJournalEntry>>([]);

        public Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
