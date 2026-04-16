using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void MultiLineLists_AreTrimmedDistinctAndStable()
    {
        AppSettings settings = new(
            CleanupExclusionPatterns: " keep.log \nkeep.log\nC:\\Temp\\DoNotTouch ",
            DriverRepositoryPaths: "D:\\Drivers\nD:\\Drivers\nE:\\Depot");

        Assert.Equal(2, settings.CleanupExclusions.Count);
        Assert.Contains("keep.log", settings.CleanupExclusions);
        Assert.Contains(@"C:\Temp\DoNotTouch", settings.CleanupExclusions);

        Assert.Equal(2, settings.DriverRepositoryRoots.Count);
        Assert.Contains(@"D:\Drivers", settings.DriverRepositoryRoots);
        Assert.Contains(@"E:\Depot", settings.DriverRepositoryRoots);
    }

    [Fact]
    public void EffectiveHealthLookbackDays_AreClampedToSafeBounds()
    {
        AppSettings settings = new(
            HealthCrashLookbackDays: -2,
            HealthWindowsUpdateLookbackDays: 120);

        Assert.Equal(7, settings.EffectiveHealthCrashLookbackDays);
        Assert.Equal(60, settings.EffectiveHealthWindowsUpdateLookbackDays);
    }

    [Fact]
    public void EffectiveAudioDefaults_AreClampedToSafeBounds()
    {
        AppSettings settings = new(
            AudioVolumeStepPercent: 100,
            AudioRecommendedVolumePercent: -1);

        Assert.Equal(25, settings.EffectiveAudioVolumeStepPercent);
        Assert.Equal(60, settings.EffectiveAudioRecommendedVolumePercent);
    }

    [Fact]
    public void EffectiveUpdateManifestUrl_IsTrimmed()
    {
        AppSettings settings = new(UpdateManifestUrl: " https://updates.ichiphost.com/aegistune/stable/stable.json ");

        Assert.Equal("https://updates.ichiphost.com/aegistune/stable/stable.json", settings.EffectiveUpdateManifestUrl);
    }
}
