namespace AegisTune.Core;

public sealed record ApplicationReviewHandoffRequest(
    string DisplayName,
    string? Publisher,
    string? RegistryKeyPath,
    AppSection SourceSection,
    string Reason,
    string SuggestedAction)
{
    public bool Matches(InstalledApplicationRecord app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!string.IsNullOrWhiteSpace(RegistryKeyPath)
            && string.Equals(RegistryKeyPath, app.RegistryKeyPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(DisplayName, app.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(Publisher)
            || string.Equals(Publisher, app.Publisher, StringComparison.OrdinalIgnoreCase);
    }

    public string SourceSectionLabel => SourceSection switch
    {
        AppSection.Repair => "Repair & Recovery",
        AppSection.Health => "Windows Health",
        AppSection.Drivers => "Drivers & Firmware",
        _ => SourceSection.ToString()
    };
}
