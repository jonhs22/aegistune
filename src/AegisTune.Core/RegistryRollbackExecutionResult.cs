namespace AegisTune.Core;

public sealed record RegistryRollbackExecutionResult(
    bool Succeeded,
    bool WasDryRun,
    string StatusLine,
    string GuidanceLine,
    DateTimeOffset ProcessedAt)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
