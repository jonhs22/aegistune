namespace AegisTune.Core;

public sealed record StartupEntryActionResult(
    bool Succeeded,
    string Message,
    DateTimeOffset ProcessedAt,
    string? ArtifactPath = null,
    bool WasDryRun = false,
    string? GuidanceLine = null)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
