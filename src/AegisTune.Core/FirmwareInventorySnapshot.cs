namespace AegisTune.Core;

public sealed record FirmwareInventorySnapshot(
    string SystemManufacturer,
    string SystemModel,
    string BaseboardManufacturer,
    string BaseboardProduct,
    string BiosManufacturer,
    string BiosVersion,
    string BiosFamilyVersion,
    DateTimeOffset? BiosReleaseDate,
    string FirmwareMode,
    bool? SecureBootEnabled,
    string SupportManufacturer,
    string SupportModel,
    string SupportIdentitySourceLabel,
    string SupportRouteLabel,
    string? PrimarySupportUrl,
    string SupportSearchHint,
    string ReadinessSummary,
    IReadOnlyList<FirmwareSupportOption> SupportOptions,
    DateTimeOffset CollectedAt,
    string? WarningMessage = null)
{
    public string SystemIdentityLabel => ComposeIdentity(SystemManufacturer, SystemModel, "System identity unavailable");

    public string BoardIdentityLabel => ComposeIdentity(BaseboardManufacturer, BaseboardProduct, "Baseboard identity unavailable");

    public string SupportIdentityLabel => ComposeIdentity(SupportManufacturer, SupportModel, "Support identity unavailable");

    public string BiosManufacturerLabel => string.IsNullOrWhiteSpace(BiosManufacturer)
        ? "BIOS vendor unknown"
        : BiosManufacturer;

    public string BiosVersionLabel => string.IsNullOrWhiteSpace(BiosVersion)
        ? "BIOS version unknown"
        : BiosVersion;

    public string BiosFamilyVersionLabel => string.IsNullOrWhiteSpace(BiosFamilyVersion)
        ? "Firmware family version unavailable"
        : BiosFamilyVersion;

    public string BiosReleaseDateLabel => BiosReleaseDate?.ToLocalTime().ToString("d") ?? "Release date unknown";

    public string BiosAgeLabel
    {
        get
        {
            if (BiosReleaseDate is null)
            {
                return "BIOS age unknown";
            }

            double totalDays = Math.Max(0, (DateTimeOffset.Now - BiosReleaseDate.Value).TotalDays);
            if (totalDays < 31)
            {
                int days = Math.Max(1, (int)Math.Round(totalDays, MidpointRounding.AwayFromZero));
                return days == 1 ? "1 day old" : $"{days:N0} days old";
            }

            if (totalDays < 730)
            {
                int months = Math.Max(1, (int)Math.Floor(totalDays / 30.4375));
                return months == 1 ? "1 month old" : $"{months:N0} months old";
            }

            int years = Math.Max(1, (int)Math.Floor(totalDays / 365.25));
            return years == 1 ? "1 year old" : $"{years:N0} years old";
        }
    }

    public string FirmwareModeLabel => string.IsNullOrWhiteSpace(FirmwareMode)
        ? "Firmware mode unknown"
        : FirmwareMode;

    public string SecureBootLabel => SecureBootEnabled switch
    {
        true => "Secure Boot enabled",
        false when string.Equals(FirmwareMode, "UEFI", StringComparison.OrdinalIgnoreCase) => "Secure Boot off",
        false => "Secure Boot not active",
        _ => "Secure Boot unknown"
    };

    public string SecurityPostureLabel => FirmwareModeLabel switch
    {
        "UEFI" when SecureBootEnabled == true => "UEFI detected with Secure Boot enabled.",
        "UEFI" => "UEFI detected. Confirm Secure Boot posture before any firmware change.",
        "Legacy BIOS" => "Legacy BIOS detected. Use only the exact board or OEM support route for firmware review.",
        _ => "Firmware mode could not be determined from Windows."
    };

    public bool HasPrimarySupportUrl => !string.IsNullOrWhiteSpace(PrimarySupportUrl);

    public string PrimarySupportUrlLabel => HasPrimarySupportUrl
        ? PrimarySupportUrl!
        : "Official support route unavailable";

    public string SupportOptionsPreview => SupportOptions.Count == 0
        ? "No firmware support steps were generated for this machine."
        : string.Join(
            Environment.NewLine,
            SupportOptions.Select((option, index) => $"{index + 1}. {option.Title}: {option.Detail}"));

    public string DashboardStatusLine => string.IsNullOrWhiteSpace(WarningMessage)
        ? ReadinessSummary
        : WarningMessage;

    public string FirmwareBrief =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"Support identity: {SupportIdentityLabel}",
                $"Identity source: {SupportIdentitySourceLabel}",
                $"System identity: {SystemIdentityLabel}",
                $"Baseboard identity: {BoardIdentityLabel}",
                $"BIOS vendor: {BiosManufacturerLabel}",
                $"BIOS version: {BiosVersionLabel}",
                $"Firmware family version: {BiosFamilyVersionLabel}",
                $"BIOS release date: {BiosReleaseDateLabel}",
                $"BIOS age: {BiosAgeLabel}",
                $"Firmware mode: {FirmwareModeLabel}",
                $"Secure Boot: {SecureBootLabel}",
                $"Firmware posture: {ReadinessSummary}",
                $"Support route: {SupportRouteLabel}",
                $"Official support URL: {PrimarySupportUrlLabel}",
                $"Search hint: {SupportSearchHint}"
            });

    private static string ComposeIdentity(string manufacturer, string model, string fallbackLabel)
    {
        string left = manufacturer?.Trim() ?? string.Empty;
        string right = model?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return fallbackLabel;
        }

        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} {right}";
    }
}
