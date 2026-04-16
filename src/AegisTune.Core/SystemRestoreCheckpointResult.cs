namespace AegisTune.Core;

public sealed record SystemRestoreCheckpointResult(
    bool Succeeded,
    string Description,
    DateTimeOffset ProcessedAt,
    string StatusLine,
    string GuidanceLine,
    int? ReturnCode = null)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
