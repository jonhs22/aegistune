namespace AegisTune.Core;

public sealed record AppSettings(
    bool DryRunEnabled = true,
    bool CreateRestorePointBeforeFixes = true,
    bool CheckForAppUpdatesOnLaunch = true,
    bool IncludeBrowserCleanup = false,
    bool PreferCompactNavigation = false,
    string UpdateManifestUrl = "https://jonhs22.github.io/aegistune/aegistune/stable/stable.json",
    string CleanupExclusionPatterns = "",
    string DriverRepositoryPaths = "",
    bool OpenExportFolderAfterExport = false,
    bool PreferReviewFirstLists = true,
    bool IncludeRegistryResidueReview = true,
    bool IncludeCrashEvidenceInHealth = true,
    bool IncludeWindowsUpdateIssuesInHealth = true,
    bool IncludeServiceReviewInHealth = true,
    bool IncludeScheduledTaskReviewInHealth = true,
    int HealthCrashLookbackDays = 7,
    int HealthWindowsUpdateLookbackDays = 14,
    int AudioVolumeStepPercent = 10,
    int AudioRecommendedVolumePercent = 60)
{
    public IReadOnlyList<string> CleanupExclusions =>
        ParseMultiLineList(CleanupExclusionPatterns);

    public IReadOnlyList<string> DriverRepositoryRoots =>
        ParseMultiLineList(DriverRepositoryPaths);

    public string EffectiveUpdateManifestUrl => UpdateManifestUrl.Trim();

    public int EffectiveHealthCrashLookbackDays => ClampLookbackDays(HealthCrashLookbackDays, 7);

    public int EffectiveHealthWindowsUpdateLookbackDays => ClampLookbackDays(HealthWindowsUpdateLookbackDays, 14);

    public int EffectiveAudioVolumeStepPercent => ClampValue(AudioVolumeStepPercent, 10, 1, 25);

    public int EffectiveAudioRecommendedVolumePercent => ClampValue(AudioRecommendedVolumePercent, 60, 20, 100);

    private static IReadOnlyList<string> ParseMultiLineList(string value) =>
        value
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int ClampLookbackDays(int value, int fallback)
    {
        if (value <= 0)
        {
            return fallback;
        }

        return Math.Clamp(value, 1, 60);
    }

    private static int ClampValue(int value, int fallback, int minimum, int maximum)
    {
        if (value <= 0)
        {
            return fallback;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}
