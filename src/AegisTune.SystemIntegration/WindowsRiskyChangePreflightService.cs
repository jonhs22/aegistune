using System.Security.Principal;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsRiskyChangePreflightService : IRiskyChangePreflightService
{
    private static readonly TimeSpan RestorePointReuseWindow = TimeSpan.FromMinutes(20);

    private readonly ISettingsStore _settingsStore;
    private readonly ISystemRestoreService _systemRestoreService;
    private readonly IUndoJournalStore _undoJournalStore;
    private readonly Func<bool> _isElevated;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CachedCheckpoint? _cachedCheckpoint;

    public WindowsRiskyChangePreflightService(
        ISettingsStore settingsStore,
        ISystemRestoreService systemRestoreService,
        IUndoJournalStore undoJournalStore)
        : this(settingsStore, systemRestoreService, undoJournalStore, IsProcessElevated)
    {
    }

    public WindowsRiskyChangePreflightService(
        ISettingsStore settingsStore,
        ISystemRestoreService systemRestoreService,
        IUndoJournalStore undoJournalStore,
        Func<bool> isElevated)
    {
        _settingsStore = settingsStore;
        _systemRestoreService = systemRestoreService;
        _undoJournalStore = undoJournalStore;
        _isElevated = isElevated;
    }

    public async Task<RiskyChangePreflightResult> PrepareAsync(
        RiskyChangePreflightRequest request,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset processedAt = DateTimeOffset.Now;
        AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);

        if (dryRunEnabled)
        {
            return new RiskyChangePreflightResult(
                true,
                false,
                false,
                true,
                processedAt,
                $"Dry-run mode is active, so AegisTune previewed {DescribeChange(request.ChangeType)} without creating a restore point.",
                settings.CreateRestorePointBeforeFixes
                    ? "A live run will try to create a Windows restore point first."
                    : "Restore-point preflight is disabled in Settings for live runs.");
        }

        if (!settings.CreateRestorePointBeforeFixes)
        {
            return new RiskyChangePreflightResult(
                true,
                false,
                false,
                false,
                processedAt,
                $"Restore-point preflight is disabled in Settings, so AegisTune will continue with {DescribeChange(request.ChangeType)}.",
                "Enable restore-point preflight in Settings if you want Windows rollback safety before risky changes.");
        }

        if (!_isElevated())
        {
            return new RiskyChangePreflightResult(
                false,
                false,
                false,
                false,
                processedAt,
                $"AegisTune blocked {DescribeChange(request.ChangeType)} because restore-point preflight requires an elevated Windows session.",
                "Run AegisTune as administrator or disable restore-point preflight in Settings before retrying.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = DateTimeOffset.Now;
            if (_cachedCheckpoint is not null && now - _cachedCheckpoint.ProcessedAt <= RestorePointReuseWindow)
            {
                return new RiskyChangePreflightResult(
                    true,
                    false,
                    true,
                    false,
                    processedAt,
                    $"Reusing the restore-point safety window created at {_cachedCheckpoint.ProcessedAtLabel} before {DescribeChange(request.ChangeType)}.",
                    "AegisTune already created a recent restore point in this session, so it does not need to create another one for every follow-up change.");
            }

            SystemRestoreCheckpointResult checkpointResult =
                await _systemRestoreService.CreateCheckpointAsync(request.Title, request.RestoreIntent, cancellationToken);

            if (checkpointResult.Succeeded)
            {
                _cachedCheckpoint = new CachedCheckpoint(checkpointResult.ProcessedAt);
                await _undoJournalStore.AppendAsync(
                    new UndoJournalEntry(
                        Guid.NewGuid(),
                        UndoJournalEntryKind.RestorePoint,
                        request.Title,
                        checkpointResult.ProcessedAt,
                        checkpointResult.StatusLine,
                        checkpointResult.GuidanceLine,
                        RestorePointCreated: true,
                        RestorePointReused: false),
                    cancellationToken);
                return new RiskyChangePreflightResult(
                    true,
                    true,
                    false,
                    false,
                    processedAt,
                    checkpointResult.StatusLine,
                    checkpointResult.GuidanceLine);
            }

            return new RiskyChangePreflightResult(
                false,
                false,
                false,
                false,
                processedAt,
                $"AegisTune blocked {DescribeChange(request.ChangeType)} because Windows restore-point creation failed. {checkpointResult.StatusLine}",
                checkpointResult.GuidanceLine);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string DescribeChange(RiskyChangeType changeType) => changeType switch
    {
        RiskyChangeType.DriverInstall => "the driver install",
        RiskyChangeType.StartupRegistryDisable => "the startup disable action",
        RiskyChangeType.StartupRegistryCleanup => "the startup cleanup action",
        RiskyChangeType.StartupRestore => "the startup restore action",
        RiskyChangeType.RegistryRepair => "the registry repair action",
        RiskyChangeType.ApplicationUninstall => "the app uninstall workflow",
        _ => "the risky change"
    };

    private static bool IsProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private sealed record CachedCheckpoint(DateTimeOffset ProcessedAt)
    {
        public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
    }
}
