using AegisTune.Core;

namespace AegisTune.Reporting;

public sealed class MaintenanceReportGenerator : IReportGenerator
{
    private readonly IDashboardSnapshotService _dashboardSnapshotService;
    private readonly IInstalledApplicationInventoryService _installedApplicationInventoryService;
    private readonly IRepairScanner _repairScanner;
    private readonly IReportStore _reportStore;

    public MaintenanceReportGenerator(
        IDashboardSnapshotService dashboardSnapshotService,
        IInstalledApplicationInventoryService installedApplicationInventoryService,
        IRepairScanner repairScanner,
        IReportStore reportStore)
    {
        _dashboardSnapshotService = dashboardSnapshotService;
        _installedApplicationInventoryService = installedApplicationInventoryService;
        _repairScanner = repairScanner;
        _reportStore = reportStore;
    }

    public async Task<MaintenanceReportRecord> GenerateAsync(CancellationToken cancellationToken = default)
    {
        DashboardSnapshot dashboardSnapshot = await _dashboardSnapshotService.GetSnapshotAsync(cancellationToken);
        AppInventorySnapshot appInventory = await _installedApplicationInventoryService.GetSnapshotAsync(cancellationToken);
        RepairScanResult repairScan = await _repairScanner.ScanAsync(appInventory: appInventory, cancellationToken: cancellationToken);

        DateTimeOffset generatedAt = DateTimeOffset.Now;
        List<ReportModuleSummary> modules =
        [
            ..dashboardSnapshot.Modules.Select(module => new ReportModuleSummary(
                module.Section,
                module.Title,
                module.StatusLine,
                module.PrimaryMetric,
                module.IssueCount))
        ];

        string fingerprint = string.Join("|",
            dashboardSnapshot.Profile.DeviceName,
            dashboardSnapshot.TotalIssueCount,
            appInventory.ApplicationCount,
            repairScan.CandidateCount,
            string.Join(";", modules.Select(module => $"{module.Section}:{module.IssueCount}:{module.PrimaryMetric}")));

        MaintenanceReportRecord report = new(
            Guid.NewGuid(),
            generatedAt,
            dashboardSnapshot.Profile.DeviceName,
            dashboardSnapshot.Profile.OperatingSystem,
            modules.Sum(module => module.IssueCount),
            modules,
            fingerprint);

        await _reportStore.SaveAsync(report, cancellationToken);
        return report;
    }
}
