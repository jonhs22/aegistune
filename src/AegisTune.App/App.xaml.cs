using AegisTune.App.Services;
using AegisTune.CleanupEngine;
using AegisTune.Core;
using AegisTune.DriverEngine;
using AegisTune.RepairEngine;
using AegisTune.Reporting;
using AegisTune.Storage;
using AegisTune.SystemIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace AegisTune.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    public static IHost Host { get; } =
        Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                services.AddSingleton<IUndoJournalStore, JsonUndoJournalStore>();
                services.AddSingleton<IReportStore, JsonReportStore>();
                services.AddSingleton<ICleanupScanner, CleanupScanner>();
                services.AddSingleton<ICleanupExecutionService, CleanupExecutionService>();
                services.AddSingleton<ISystemRestoreService, WindowsSystemRestoreService>();
                services.AddSingleton<IRiskyChangePreflightService, WindowsRiskyChangePreflightService>();
                services.AddSingleton<IRegistryBackupService, WindowsRegistryBackupService>();
                services.AddSingleton<IRegistryRepairEvidenceService, WindowsRegistryRepairEvidenceService>();
                services.AddSingleton<IRegistryRepairExecutionService, WindowsRegistryRepairExecutionService>();
                services.AddSingleton<IRegistryRollbackService, WindowsRegistryRollbackService>();
                services.AddSingleton<IDeviceInventoryService, WindowsDeviceInventoryService>();
                services.AddSingleton<IDriverDepotService, LocalDriverDepotService>();
                services.AddSingleton<IDriverAuditExportService, FileDriverAuditExportService>();
                services.AddSingleton<IDriverInstallService, PnpUtilDriverInstallService>();
                services.AddSingleton<IDriverInstallVerificationService, DriverInstallVerificationService>();
                services.AddSingleton<IDriverStoreEvidenceService, PnpUtilDriverStoreEvidenceService>();
                services.AddSingleton<IDriverRepositorySeedService, PnpUtilDriverRepositorySeedService>();
                services.AddSingleton<IDriverCommandRunner, ElevatedDriverCommandRunner>();
                services.AddSingleton<IDriverQueryRunner, ProcessDriverQueryRunner>();
                services.AddSingleton<IDriverRemediationExportService, FileDriverRemediationExportService>();
                services.AddSingleton<IInstalledApplicationInventoryService, WindowsInstalledApplicationInventoryService>();
                services.AddSingleton<IApplicationUninstallService, WindowsApplicationUninstallService>();
                services.AddSingleton<IApplicationResidueCleanupService, WindowsApplicationResidueCleanupService>();
                services.AddSingleton<IRepairEvidenceService, WindowsRepairEvidenceService>();
                services.AddSingleton<IAudioPlatformAdapter, WindowsAudioPlatformAdapter>();
                services.AddSingleton<IAudioInventoryService, WindowsAudioInventoryService>();
                services.AddSingleton<IAudioControlService, WindowsAudioControlService>();
                services.AddSingleton<IAppUpdateService, AppUpdateService>();
                services.AddSingleton<IWindowsHealthService, WindowsHealthService>();
                services.AddSingleton<IRepairAdvisoryExportService, FileRepairAdvisoryExportService>();
                services.AddSingleton<IRepairScanner, EvidenceBasedRepairScanner>();
                services.AddSingleton<IReportExportService, FileReportExportService>();
                services.AddSingleton<IStartupInventoryService, WindowsStartupInventoryService>();
                services.AddSingleton<IStartupEntryActionService, WindowsStartupEntryActionService>();
                services.AddSingleton<IFirmwareInventoryService, WindowsFirmwareInventoryService>();
                services.AddSingleton<IBitLockerStatusProbe, WindowsBitLockerStatusProbe>();
                services.AddSingleton<IPowerStatusProbe, WindowsPowerStatusProbe>();
                services.AddSingleton<IFirmwareReleaseLookupService, OfficialFirmwareReleaseLookupService>();
                services.AddSingleton<IFirmwareSafetyAssessmentService, WindowsFirmwareSafetyAssessmentService>();
                services.AddSingleton<ISystemProfileService, WindowsSystemProfileService>();
                services.AddSingleton<IDashboardSnapshotService, DashboardSnapshotService>();
                services.AddSingleton<IReportGenerator, MaintenanceReportGenerator>();
                services.AddSingleton<IApplicationReviewHandoffService, ApplicationReviewHandoffService>();
                services.AddSingleton<AppReleaseNotesDialogService>();
                services.AddSingleton<AppNavigationService>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

    public static T GetService<T>()
        where T : notnull =>
        Host.Services.GetRequiredService<T>();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Host.StartAsync();
        _window = GetService<MainWindow>();
        _window.Activate();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ILogger<App>? logger = Host.Services.GetService<ILogger<App>>();
        logger?.LogError(e.Exception, "Unhandled application exception.");
    }
}
