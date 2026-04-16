namespace AegisTune.Core;

public sealed record FirmwareReleaseLookupResult(
    FirmwareReleaseLookupMode Mode,
    string VendorLabel,
    string ModelLabel,
    string CurrentVersion,
    DateTimeOffset? CurrentReleaseDate,
    string StatusLine,
    string GuidanceLine,
    string ComparisonSummary,
    string SearchHint,
    string? SupportUrl,
    string? DetailsUrl,
    string? LatestVersion = null,
    DateTimeOffset? LatestReleaseDate = null,
    string? ToolTitle = null,
    string? ToolDetail = null,
    string? EvidenceSource = null,
    string? WarningMessage = null,
    DateTimeOffset? CheckedAt = null,
    bool LatestIsBeta = false,
    string? LatestReleaseTitle = null,
    string? LatestReleaseNotesSummary = null)
{
    public bool WasChecked => CheckedAt is not null;

    public bool HasLatestRelease => !string.IsNullOrWhiteSpace(LatestVersion);

    public bool HasSupportUrl => !string.IsNullOrWhiteSpace(SupportUrl);

    public bool HasDetailsUrl => !string.IsNullOrWhiteSpace(DetailsUrl);

    public string LatestVersionLabel => HasLatestRelease
        ? LatestIsBeta
            ? $"{LatestVersion!} (Beta)"
            : LatestVersion!
        : "Latest BIOS version not verified yet";

    public string LatestReleaseTitleLabel => !string.IsNullOrWhiteSpace(LatestReleaseTitle)
        ? LatestReleaseTitle!
        : LatestVersionLabel;

    public string LatestReleaseNotesSummaryLabel => !string.IsNullOrWhiteSpace(LatestReleaseNotesSummary)
        ? LatestReleaseNotesSummary!
        : "No release-note preview was cached from the official source.";

    public string LatestReleaseDateLabel => LatestReleaseDate?.ToLocalTime().ToString("d") ?? "Latest release date not verified yet";

    public string SupportUrlLabel => HasSupportUrl ? SupportUrl! : "No official support URL is available for this lookup result";

    public string DetailsUrlLabel => HasDetailsUrl ? DetailsUrl! : SupportUrlLabel;

    public string ToolTitleLabel => string.IsNullOrWhiteSpace(ToolTitle) ? "No vendor tool workflow required" : ToolTitle!;

    public string ToolDetailLabel => string.IsNullOrWhiteSpace(ToolDetail) ? "No vendor-specific tool detail is attached to this lookup result." : ToolDetail!;

    public string EvidenceSourceLabel => string.IsNullOrWhiteSpace(EvidenceSource) ? "Evidence source unavailable" : EvidenceSource!;

    public string CheckedAtLabel => CheckedAt?.ToLocalTime().ToString("g") ?? "Lookup not run yet";

    public string ModeLabel => Mode switch
    {
        FirmwareReleaseLookupMode.DirectVendorPage => "Direct vendor page",
        FirmwareReleaseLookupMode.VendorSupportSearch => "Vendor support search",
        FirmwareReleaseLookupMode.VendorToolWorkflow => "Vendor utility workflow",
        FirmwareReleaseLookupMode.CatalogFeed => "Catalog feed",
        FirmwareReleaseLookupMode.ManualReview => "Manual vendor review",
        FirmwareReleaseLookupMode.LookupFailed => "Lookup failed",
        _ => "Not checked"
    };

    public string ClipboardText =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"Vendor: {VendorLabel}",
                $"Model: {ModelLabel}",
                $"Lookup mode: {ModeLabel}",
                $"Current BIOS version: {CurrentVersion}",
                $"Current BIOS release date: {CurrentReleaseDate?.ToLocalTime().ToString("d") ?? "Unknown"}",
                $"Latest BIOS version: {LatestVersionLabel}",
                $"Latest BIOS release date: {LatestReleaseDateLabel}",
                $"Latest release title: {LatestReleaseTitleLabel}",
                $"Latest release-notes preview: {LatestReleaseNotesSummaryLabel}",
                $"Status: {StatusLine}",
                $"Guidance: {GuidanceLine}",
                $"Comparison: {ComparisonSummary}",
                $"Evidence source: {EvidenceSourceLabel}",
                $"Support URL: {SupportUrlLabel}",
                $"Details URL: {DetailsUrlLabel}",
                $"Search hint: {SearchHint}",
                $"Vendor tool: {ToolTitleLabel}",
                $"Vendor tool detail: {ToolDetailLabel}",
                $"Checked at: {CheckedAtLabel}"
            });
}
