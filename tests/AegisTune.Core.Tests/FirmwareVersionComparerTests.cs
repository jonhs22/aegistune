using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class FirmwareVersionComparerTests
{
    [Theory]
    [InlineData("N47ET17W (1.17)", "1.17")]
    [InlineData("4645", "4645")]
    [InlineData("A24", "A24")]
    public void AreEquivalent_ReturnsTrueForEquivalentVendorAndDisplayVersions(string currentVersion, string latestVersion)
    {
        Assert.True(FirmwareVersionComparer.AreEquivalent(currentVersion, latestVersion));
    }

    [Theory]
    [InlineData("1.16", "1.17")]
    [InlineData("1.2.0", "1.20.0")]
    [InlineData("A23", "A24")]
    public void AreEquivalent_ReturnsFalseForDifferentVersions(string currentVersion, string latestVersion)
    {
        Assert.False(FirmwareVersionComparer.AreEquivalent(currentVersion, latestVersion));
    }
}
