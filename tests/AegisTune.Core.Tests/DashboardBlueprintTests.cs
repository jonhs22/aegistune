using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class DashboardBlueprintTests
{
    [Fact]
    public void Create_BuildsDynamicSnapshotFromRealScanResults()
    {
        SystemProfile profile = new("WS-01", "Windows 11 Pro", "Build 26300", IsAdministrator: true);
        AppSettings settings = new(
            DryRunEnabled: false,
            CreateRestorePointBeforeFixes: true,
            IncludeBrowserCleanup: true,
            PreferCompactNavigation: false);
        DateTimeOffset now = new(2026, 4, 15, 10, 30, 0, TimeSpan.Zero);

        CleanupScanResult cleanupScan = new(
            new[]
            {
                new CleanupTargetScanResult("User temp", "Per-user temp files.", "%TEMP%", CleanupTargetStatus.Ready, true, 125, 512 * 1024 * 1024),
                new CleanupTargetScanResult("System temp", "Windows temp files.", @"C:\Windows\Temp", CleanupTargetStatus.Ready, true, 11, 128 * 1024 * 1024),
                new CleanupTargetScanResult("Recycle Bin", "Reclaimable recycle bin content.", @"C:\$Recycle.Bin", CleanupTargetStatus.Empty, true, 0, 0)
            },
            now);

        DeviceInventorySnapshot deviceInventory = new(
            new[]
            {
                new DriverDeviceRecord(
                    "Intel Wi-Fi 6 AX201",
                    "Net",
                    "Intel",
                    "Intel",
                    "23.10.0.8",
                    "OK",
                    0,
                    "PCI\\VEN_8086",
                    HardwareIds:
                    [
                        "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
                    ]),
                new DriverDeviceRecord("Generic Bluetooth Adapter", "Bluetooth", "Microsoft", "", "", "Error", 28, "USB\\VID_0A12")
            },
            now);

        StartupInventorySnapshot startupInventory = new(
            new[]
            {
                new StartupEntryRecord("OneDrive", "\"C:\\Program Files\\Microsoft OneDrive\\OneDrive.exe\" /background", "Registry Run", "Current user", @"C:\Program Files\Microsoft OneDrive\OneDrive.exe", true, false, StartupImpactLevel.High),
                new StartupEntryRecord("Old Helper", "\"C:\\Missing\\helper.exe\"", "Registry Run", "Current user", @"C:\Missing\helper.exe", false, true, StartupImpactLevel.Review)
            },
            now);

        AppInventorySnapshot appInventory = new(
            new[]
            {
                new InstalledApplicationRecord(
                    "Contoso Cleanup",
                    "4.2",
                    "Contoso",
                    InstalledApplicationSource.DesktopRegistry,
                    "Current user",
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup",
                    @"C:\Missing\Contoso Cleanup",
                    false,
                    "\"C:\\Missing\\ContosoCleanup\\uninstall.exe\"",
                    @"C:\Missing\ContosoCleanup\uninstall.exe",
                    false,
                    512L * 1024 * 1024),
                new InstalledApplicationRecord(
                    "Photos",
                    "2026.1",
                    "Microsoft",
                    InstalledApplicationSource.Packaged,
                    "Current user",
                    "Microsoft.Windows.Photos_2026.1_x64__8wekyb3d8bbwe",
                    null,
                    false,
                    null,
                    null,
                    false,
                    null)
            },
            now);

        RepairScanResult repairScan = new(
            new[]
            {
                new RepairCandidateRecord(
                    "Remove orphaned startup entry: Old Helper",
                    "Startup",
                    RiskLevel.Safe,
                    false,
                    @"Registry Run still points to a missing target: C:\Missing\helper.exe",
                    "Remove the stale startup entry after one final review.",
                    "Registry Run"),
                new RepairCandidateRecord(
                    "Review uninstall leftovers: Contoso Cleanup",
                    "Apps",
                    RiskLevel.Review,
                    false,
                    "Install folder is missing and uninstall target is unavailable for Contoso Cleanup.",
                    "Inspect the stale uninstall registration before removing leftovers.",
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup")
            },
            now);

        DashboardSnapshot snapshot = DashboardBlueprint.Create(
            profile,
            CreateFirmwareSnapshot(now),
            CreateHealthSnapshot(now),
            CreateAudioSnapshot(now),
            settings,
            cleanupScan,
            deviceInventory,
            startupInventory,
            appInventory,
            repairScan,
            3,
            now);

        Assert.Equal(profile, snapshot.Profile);
        Assert.Equal("ASUS", snapshot.Firmware.SupportManufacturer);
        Assert.Equal(settings, snapshot.Settings);
        Assert.Equal(8, snapshot.Modules.Count);
        Assert.Equal(8, snapshot.TotalIssueCount);
        Assert.Equal(8, snapshot.ReadyModuleCount);
        Assert.Equal(5, snapshot.ReviewModuleCount);

        ModuleSnapshot health = Assert.Single(snapshot.Modules.Where(module => module.Section == AppSection.Health));
        Assert.Equal("0 items", health.PrimaryMetric);
        Assert.Contains("active Windows health review scope", health.StatusLine);

        ModuleSnapshot audio = Assert.Single(snapshot.Modules.Where(module => module.Section == AppSection.Audio));
        Assert.Equal("2 endpoints", audio.PrimaryMetric);
        Assert.Contains("Default playback and recording devices are available", audio.StatusLine);

        ModuleSnapshot cleaner = Assert.Single(snapshot.Modules.Where(module => module.Section == AppSection.Cleaner));
        Assert.Equal("640.0 MB", cleaner.PrimaryMetric);
        Assert.Contains("640.0 MB", cleaner.StatusLine);

        ModuleSnapshot apps = Assert.Single(snapshot.Modules.Where(module => module.Section == AppSection.Apps));
        Assert.Equal("2 apps", apps.PrimaryMetric);
        Assert.Contains("1 uninstall leftover candidate", apps.StatusLine);

        ModuleSnapshot drivers = Assert.Single(snapshot.Modules.Where(module => module.Section == AppSection.Drivers));
        Assert.Contains("compatible-ID fallback", drivers.StatusLine);
        Assert.Contains("Firmware route", drivers.StatusLine);

        ModuleSnapshot reports = Assert.Single(snapshot.Modules.Where(module => module.Section == AppSection.Reports));
        Assert.Equal("3 reports", reports.PrimaryMetric);
    }

    [Fact]
    public void Create_ReflectsDryRunSettingInCleanerStatus()
    {
        DateTimeOffset now = new(2026, 4, 15, 11, 00, 0, TimeSpan.Zero);

        DashboardSnapshot snapshot = DashboardBlueprint.Create(
            new SystemProfile("WS-02", "Windows 11 Pro", "Build 26300", IsAdministrator: false),
            CreateFirmwareSnapshot(now),
            CreateHealthSnapshot(now),
            CreateAudioSnapshot(now),
            new AppSettings(DryRunEnabled: true),
            new CleanupScanResult(
                new[]
                {
                    new CleanupTargetScanResult("User temp", "Per-user temp files.", "%TEMP%", CleanupTargetStatus.Empty, true, 0, 0)
                },
                now),
            new DeviceInventorySnapshot(Array.Empty<DriverDeviceRecord>(), now),
            new StartupInventorySnapshot(Array.Empty<StartupEntryRecord>(), now),
            new AppInventorySnapshot(Array.Empty<InstalledApplicationRecord>(), now),
            new RepairScanResult(Array.Empty<RepairCandidateRecord>(), now),
            0,
            now);

        ModuleSnapshot cleaner = Assert.Single(
            snapshot.Modules.Where(module => module.Section == AppSection.Cleaner));

        Assert.Contains("Dry-run mode is active", cleaner.StatusLine);
        Assert.Equal("Safe", cleaner.RiskLabel);
        Assert.Equal("Operational", cleaner.ReadinessLabel);
    }

    private static FirmwareInventorySnapshot CreateFirmwareSnapshot(DateTimeOffset collectedAt) =>
        FirmwareSupportAdvisor.Build(
            "System manufacturer",
            "System Product Name",
            "ASUSTeK COMPUTER INC.",
            "TUF B450-PLUS GAMING",
            "American Megatrends Inc.",
            "4645",
            "ALASKA - 1072009",
            new DateTimeOffset(2026, 1, 5, 2, 0, 0, TimeSpan.Zero),
            "UEFI",
            true,
            collectedAt);

    private static WindowsHealthSnapshot CreateHealthSnapshot(DateTimeOffset scannedAt) =>
        new(
            Array.Empty<WindowsHealthEventRecord>(),
            Array.Empty<WindowsHealthEventRecord>(),
            Array.Empty<ServiceReviewRecord>(),
            Array.Empty<ScheduledTaskReviewRecord>(),
            scannedAt);

    private static AudioInventorySnapshot CreateAudioSnapshot(DateTimeOffset collectedAt) =>
        new(
            [
                new AudioEndpointRecord(
                    "playback-default",
                    "Speakers (Realtek Audio)",
                    AudioEndpointKind.Playback,
                    true,
                    false,
                    70,
                    false,
                    "Active")
            ],
            [
                new AudioEndpointRecord(
                    "recording-default",
                    "Microphone (USB Audio)",
                    AudioEndpointKind.Recording,
                    true,
                    false,
                    65,
                    false,
                    "Active")
            ],
            60,
            collectedAt);
}
