namespace AegisTune.Core;

public sealed record AppReleaseNotesState(
    bool HasLoaded,
    bool IsAvailable,
    string Title,
    string Content,
    string SourceUrl = "",
    string StatusLine = "",
    DateTimeOffset? LoadedAt = null)
{
    public string LoadedAtLabel => LoadedAt?.ToLocalTime().ToString("g") ?? "Not loaded yet";

    public bool HasSourceUrl => !string.IsNullOrWhiteSpace(SourceUrl);

    public static AppReleaseNotesState CreateInitial() =>
        new(
            false,
            false,
            "Release notes",
            "Release notes have not been loaded yet.",
            string.Empty,
            "Open the update section and choose View release notes.");
}
