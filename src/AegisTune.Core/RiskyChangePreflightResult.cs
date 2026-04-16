namespace AegisTune.Core;

public sealed record RiskyChangePreflightResult(
    bool ShouldProceed,
    bool RestorePointCreated,
    bool RestorePointReused,
    bool WasDryRun,
    DateTimeOffset ProcessedAt,
    string StatusLine,
    string GuidanceLine)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
