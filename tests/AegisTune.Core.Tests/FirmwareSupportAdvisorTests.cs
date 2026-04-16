using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class FirmwareSupportAdvisorTests
{
    [Fact]
    public void Build_FallsBackToBaseboardIdentity_WhenSystemStringsAreGeneric()
    {
        FirmwareInventorySnapshot snapshot = FirmwareSupportAdvisor.Build(
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
            new DateTimeOffset(2026, 4, 16, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal("Baseboard fallback identity", snapshot.SupportIdentitySourceLabel);
        Assert.Equal("ASUS", snapshot.SupportManufacturer);
        Assert.Equal("TUF B450-PLUS GAMING", snapshot.SupportModel);
        Assert.Equal("Official ASUS firmware route", snapshot.SupportRouteLabel);
        Assert.Equal("https://www.asus.com/support/", snapshot.PrimarySupportUrl);
        Assert.Contains("TUF B450-PLUS GAMING BIOS 4645", snapshot.SupportSearchHint);
    }

    [Theory]
    [InlineData("Dell Inc.", "Dell", "https://www.dell.com/support/home")]
    [InlineData("LENOVO", "Lenovo", "https://pcsupport.lenovo.com/")]
    [InlineData("Hewlett-Packard", "HP", "https://support.hp.com/")]
    public void Build_UsesMappedOfficialVendorRoutes(string manufacturer, string expectedSupportManufacturer, string expectedUrl)
    {
        FirmwareInventorySnapshot snapshot = FirmwareSupportAdvisor.Build(
            manufacturer,
            "Business Platform 15",
            null,
            null,
            "Vendor BIOS",
            "1.14.2",
            "Firmware family",
            new DateTimeOffset(2025, 10, 12, 0, 0, 0, TimeSpan.Zero),
            "UEFI",
            false,
            new DateTimeOffset(2026, 4, 16, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal(expectedSupportManufacturer, snapshot.SupportManufacturer);
        Assert.Equal(expectedUrl, snapshot.PrimarySupportUrl);
        Assert.Contains("official", snapshot.SupportSearchHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_LeavesUnknownVendorOnManualReviewRoute_WhenNoOfficialMapExists()
    {
        FirmwareInventorySnapshot snapshot = FirmwareSupportAdvisor.Build(
            "Contoso Boards",
            "CX-9000",
            null,
            null,
            "Contoso BIOS",
            "7.1.0",
            "Family version",
            null,
            "Legacy BIOS",
            false,
            new DateTimeOffset(2026, 4, 16, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal("Contoso Boards", snapshot.SupportManufacturer);
        Assert.Null(snapshot.PrimarySupportUrl);
        Assert.Equal("Manual firmware review route", snapshot.SupportRouteLabel);
        Assert.Contains("manual vendor-review path", snapshot.ReadinessSummary);
        Assert.Contains("Legacy BIOS", snapshot.SecurityPostureLabel);
    }
}
