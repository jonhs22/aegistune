namespace AegisTune.Core;

public sealed record AppUpdateState(
    string CurrentVersion,
    AppDistributionKind DistributionKind,
    bool AutomaticChecksEnabled,
    bool HasChecked,
    bool IsUpdateAvailable,
    string StatusLine,
    string GuidanceLine,
    string FeedUrl = "",
    string LatestVersion = "",
    string? PortablePackageUrl = null,
    string? MsixPackageUrl = null,
    string? AppInstallerUrl = null,
    string? ReleaseNotesUrl = null,
    DateTimeOffset? CheckedAt = null)
{
    public string DistributionLabel => DistributionKind == AppDistributionKind.Packaged
        ? "MSIX install"
        : "Portable build";

    public string LatestVersionLabel => string.IsNullOrWhiteSpace(LatestVersion)
        ? "No published version detected yet"
        : LatestVersion;

    public string CheckedAtLabel => CheckedAt?.ToLocalTime().ToString("g") ?? "Not checked yet";

    public string PreferredUpdateUrl => DistributionKind == AppDistributionKind.Packaged
        ? FirstNonEmpty(AppInstallerUrl, MsixPackageUrl, FeedUrl)
        : FirstNonEmpty(PortablePackageUrl, FeedUrl);

    public bool CanOpenPreferredUpdateUrl => !string.IsNullOrWhiteSpace(PreferredUpdateUrl);

    public bool HasReleaseNotesUrl => !string.IsNullOrWhiteSpace(ReleaseNotesUrl);

    public string PrimaryActionLabel
    {
        get
        {
            if (IsUpdateAvailable)
            {
                return DistributionKind == AppDistributionKind.Packaged
                    ? "Open MSIX update source"
                    : "Download portable update";
            }

            return "Open update source";
        }
    }

    public static AppUpdateState CreateInitial(string currentVersion, AppDistributionKind distributionKind) =>
        new(
            currentVersion,
            distributionKind,
            true,
            false,
            false,
            "App updates have not been checked yet.",
            "Open Home or Settings to check the configured update feed.",
            string.Empty);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
