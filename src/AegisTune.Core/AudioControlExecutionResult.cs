namespace AegisTune.Core;

public sealed record AudioControlExecutionResult(
    string EndpointName,
    string ActionLabel,
    bool WasDryRun,
    bool Succeeded,
    int? VolumePercent,
    bool? IsMuted,
    DateTimeOffset ProcessedAt,
    string StatusLine,
    string GuidanceLine)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
