using AegisTune.Core;
using Microsoft.Extensions.Logging;

namespace AegisTune.App.Services;

public sealed class DashboardSnapshotService : IDashboardSnapshotService
{
    private readonly ICleanupScanner _cleanupScanner;
    private readonly IDeviceInventoryService _deviceInventoryService;
    private readonly IInstalledApplicationInventoryService _installedApplicationInventoryService;
    private readonly IRepairScanner _repairScanner;
    private readonly IReportStore _reportStore;
    private readonly ISettingsStore _settingsStore;
    private readonly IStartupInventoryService _startupInventoryService;
    private readonly IAudioInventoryService _audioInventoryService;
    private readonly IFirmwareInventoryService _firmwareInventoryService;
    private readonly IWindowsHealthService _windowsHealthService;
    private readonly ISystemProfileService _systemProfileService;
    private readonly ILogger<DashboardSnapshotService> _logger;

    public DashboardSnapshotService(
        ICleanupScanner cleanupScanner,
        IDeviceInventoryService deviceInventoryService,
        IInstalledApplicationInventoryService installedApplicationInventoryService,
        IRepairScanner repairScanner,
        IReportStore reportStore,
        ISettingsStore settingsStore,
        IStartupInventoryService startupInventoryService,
        IAudioInventoryService audioInventoryService,
        IFirmwareInventoryService firmwareInventoryService,
        IWindowsHealthService windowsHealthService,
        ISystemProfileService systemProfileService,
        ILogger<DashboardSnapshotService> logger)
    {
        _cleanupScanner = cleanupScanner;
        _deviceInventoryService = deviceInventoryService;
        _installedApplicationInventoryService = installedApplicationInventoryService;
        _repairScanner = repairScanner;
        _reportStore = reportStore;
        _settingsStore = settingsStore;
        _startupInventoryService = startupInventoryService;
        _audioInventoryService = audioInventoryService;
        _firmwareInventoryService = firmwareInventoryService;
        _windowsHealthService = windowsHealthService;
        _systemProfileService = systemProfileService;
        _logger = logger;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);
        SystemProfile profile = _systemProfileService.GetCurrentProfile();
        Task<CleanupScanResult> cleanupTask = _cleanupScanner.ScanAsync(cancellationToken);
        Task<DeviceInventorySnapshot> deviceInventoryTask = _deviceInventoryService.GetSnapshotAsync(cancellationToken);
        Task<StartupInventorySnapshot> startupInventoryTask = _startupInventoryService.GetSnapshotAsync(cancellationToken);
        Task<AudioInventorySnapshot> audioInventoryTask = _audioInventoryService.GetSnapshotAsync(cancellationToken);
        Task<AppInventorySnapshot> appInventoryTask = _installedApplicationInventoryService.GetSnapshotAsync(cancellationToken);
        Task<FirmwareInventorySnapshot> firmwareTask = _firmwareInventoryService.GetSnapshotAsync(cancellationToken);
        Task<WindowsHealthSnapshot> windowsHealthTask = _windowsHealthService.GetSnapshotAsync(cancellationToken);
        Task<IReadOnlyList<MaintenanceReportRecord>> reportHistoryTask = _reportStore.LoadAsync(cancellationToken);

        await Task.WhenAll(cleanupTask, deviceInventoryTask, startupInventoryTask, audioInventoryTask, appInventoryTask, firmwareTask, windowsHealthTask, reportHistoryTask);

        StartupInventorySnapshot startupInventory = await startupInventoryTask;
        AppInventorySnapshot appInventory = await appInventoryTask;
        RepairScanResult repairScan = await _repairScanner.ScanAsync(appInventory, startupInventory, cancellationToken);

        DashboardSnapshot snapshot = DashboardBlueprint.Create(
            profile,
            await firmwareTask,
            await windowsHealthTask,
            await audioInventoryTask,
            settings,
            await cleanupTask,
            await deviceInventoryTask,
            startupInventory,
            appInventory,
            repairScan,
            (await reportHistoryTask).Count,
            DateTimeOffset.Now);
        _logger.LogInformation(
            "Loaded dashboard snapshot for {DeviceName} on {BuildLabel} with {TotalIssueCount} total findings and firmware route {SupportIdentity}.",
            profile.DeviceName,
            profile.BuildLabel,
            snapshot.TotalIssueCount,
            snapshot.Firmware.SupportIdentityLabel);

        return snapshot;
    }
}
