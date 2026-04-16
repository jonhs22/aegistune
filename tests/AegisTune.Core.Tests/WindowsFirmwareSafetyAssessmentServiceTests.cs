using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class WindowsFirmwareSafetyAssessmentServiceTests
{
    [Fact]
    public async Task AssessAsync_BlocksWhenCurrentVersionMatchesLatestOfficialListing()
    {
        WindowsFirmwareSafetyAssessmentService service = new(
            new StubBitLockerStatusProbe(new BitLockerVolumeStatus(false, "BitLocker is off.", "Protection is not active.")),
            new StubPowerStatusProbe(new SystemPowerSnapshot(true, false, null, "AC power is connected.", "External power is stable.")));

        FirmwareSafetyAssessment assessment = await service.AssessAsync(
            CreateFirmwareSnapshot("System model identity"),
            new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.DirectVendorPage,
                "ASUS",
                "ASUS TUF B450-PLUS GAMING",
                "4645",
                new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
                "Latest BIOS verified.",
                "Use the official page.",
                "Current BIOS 4645 matches the latest official listing.",
                "Search the ASUS support page.",
                "https://www.asus.com/support/",
                "https://www.asus.com/support/",
                "4645",
                new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
                "MyASUS",
                "Keep vendor review explicit.",
                "Official ASUS support page",
                null,
                new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero)));

        Assert.True(assessment.HasBlockingGate);
        Assert.Equal("Blocked until safety gates are cleared", assessment.OverallPostureLabel);
        Assert.Contains(
            assessment.Gates,
            gate => gate.Title == "Official source comparison"
                && gate.Severity == FirmwareSafetyGateSeverity.Block);
    }

    [Fact]
    public async Task AssessAsync_BlocksWhenCurrentVersionContainsEquivalentLenovoDisplayVersion()
    {
        WindowsFirmwareSafetyAssessmentService service = new(
            new StubBitLockerStatusProbe(new BitLockerVolumeStatus(false, "BitLocker is off.", "Protection is not active.")),
            new StubPowerStatusProbe(new SystemPowerSnapshot(true, false, null, "AC power is connected.", "External power is stable.")));

        FirmwareInventorySnapshot firmware = CreateFirmwareSnapshot("System model identity") with
        {
            SystemManufacturer = "LENOVO",
            SystemModel = "ThinkPad T14 Gen 5 21MM",
            BaseboardManufacturer = "LENOVO",
            BaseboardProduct = "21MM",
            BiosManufacturer = "LENOVO",
            BiosVersion = "N47ET17W (1.17)",
            BiosFamilyVersion = "N47ET17W",
            SupportManufacturer = "Lenovo",
            SupportModel = "ThinkPad T14 Gen 5 21MM",
            SupportRouteLabel = "Official Lenovo firmware route",
            PrimarySupportUrl = "https://pcsupport.lenovo.com/us/en/search",
            SupportSearchHint = "Search Lenovo support for ThinkPad T14 Gen 5 21MM before any firmware change.",
            ReadinessSummary = "Firmware review is aligned to ThinkPad T14 Gen 5 21MM."
        };

        FirmwareSafetyAssessment assessment = await service.AssessAsync(
            firmware,
            new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.CatalogFeed,
                "Lenovo",
                "T14 Gen 5 (Type 21ML, 21MM) Laptops (ThinkPad) - Type 21MM",
                "N47ET17W (1.17)",
                new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                "Latest BIOS verified.",
                "Use Lenovo Support.",
                "Current BIOS N47ET17W (1.17) matches the latest BIOS listing.",
                "Search Lenovo support.",
                "https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkpad-t-series-laptops/thinkpad-t14-gen-5-type-21ml-21mm/21mm/downloads/driver-list",
                "https://download.lenovo.com/pccbbs/mobiles/n47uj12w.html",
                "1.17",
                new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                "Lenovo Vantage / System Update",
                "Keep vendor review explicit.",
                "Official Lenovo Support search and downloads API",
                null,
                new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero)));

        Assert.Contains(
            assessment.Gates,
            gate => gate.Title == "Official source comparison"
                && gate.Severity == FirmwareSafetyGateSeverity.Block);
    }

    [Fact]
    public async Task AssessAsync_RaisesBitLockerAndBatterySafetyGates()
    {
        WindowsFirmwareSafetyAssessmentService service = new(
            new StubBitLockerStatusProbe(new BitLockerVolumeStatus(true, "BitLocker protection is active on C:.", "Firmware updates can trigger recovery when protection is left active.")),
            new StubPowerStatusProbe(new SystemPowerSnapshot(false, true, 37, "System is running on battery at 37%.", "Flashing on battery is unsafe.")));

        FirmwareSafetyAssessment assessment = await service.AssessAsync(
            CreateFirmwareSnapshot("Baseboard fallback identity"),
            new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.DirectVendorPage,
                "ASUS",
                "ASUS TUF B450-PLUS GAMING",
                "4204",
                new DateTimeOffset(2025, 4, 2, 0, 0, 0, TimeSpan.Zero),
                "Latest BIOS verified.",
                "The latest official listing is Beta.",
                "Official ASUS support lists BIOS 4645 and marks it Beta.",
                "Search the ASUS support page.",
                "https://www.asus.com/support/",
                "https://www.asus.com/support/",
                "4645",
                new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
                "MyASUS",
                "Keep vendor review explicit.",
                "Official ASUS support page",
                "Latest listing is Beta.",
                new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero),
                true));

        Assert.True(assessment.HasBlockingGate);
        Assert.True(assessment.HasAttentionGate);
        Assert.Contains(
            assessment.Gates,
            gate => gate.Title == "BitLocker posture"
                && gate.Severity == FirmwareSafetyGateSeverity.Attention);
        Assert.Contains(
            assessment.Gates,
            gate => gate.Title == "Power stability"
                && gate.Severity == FirmwareSafetyGateSeverity.Block);
        Assert.Contains(
            assessment.Gates,
            gate => gate.Title == "Identity source"
                && gate.Severity == FirmwareSafetyGateSeverity.Attention);
    }

    [Fact]
    public async Task AssessAsync_BlocksGenericIdentityUntilManualModelConfirmation()
    {
        WindowsFirmwareSafetyAssessmentService service = new(
            new StubBitLockerStatusProbe(new BitLockerVolumeStatus(false, "BitLocker is off.", "Protection is not active.")),
            new StubPowerStatusProbe(new SystemPowerSnapshot(true, false, null, "AC power is connected.", "External power is stable.")));

        FirmwareSafetyAssessment assessment = await service.AssessAsync(CreateFirmwareSnapshot("Generic machine identity"));

        Assert.Contains(
            assessment.Gates,
            gate => gate.Title == "Identity source"
                && gate.Severity == FirmwareSafetyGateSeverity.Block);
    }

    private static FirmwareInventorySnapshot CreateFirmwareSnapshot(string supportIdentitySourceLabel) =>
        new(
            "ASUS",
            "TUF B450-PLUS GAMING",
            "ASUSTeK COMPUTER INC.",
            "TUF B450-PLUS GAMING",
            "American Megatrends International, LLC.",
            "4645",
            "TUF B450-PLUS GAMING BIOS 4645",
            new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
            "UEFI",
            true,
            "ASUS",
            "TUF B450-PLUS GAMING",
            supportIdentitySourceLabel,
            "Official ASUS firmware route",
            "https://www.asus.com/support/",
            "Search the official ASUS support page for \"TUF B450-PLUS GAMING BIOS 4645\" before any firmware change.",
            "Firmware review is aligned to TUF B450-PLUS GAMING.",
            Array.Empty<FirmwareSupportOption>(),
            new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero));

    private sealed class StubBitLockerStatusProbe(BitLockerVolumeStatus status) : IBitLockerStatusProbe
    {
        public Task<BitLockerVolumeStatus> GetSystemDriveStatusAsync(string systemDrive, CancellationToken cancellationToken = default) =>
            Task.FromResult(status);
    }

    private sealed class StubPowerStatusProbe(SystemPowerSnapshot status) : IPowerStatusProbe
    {
        public Task<SystemPowerSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(status);
    }
}
