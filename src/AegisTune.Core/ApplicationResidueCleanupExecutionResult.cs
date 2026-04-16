namespace AegisTune.Core;

public sealed record ApplicationResidueCleanupExecutionResult(
    string DisplayName,
    bool WasDryRun,
    bool Succeeded,
    int MovedFolderCount,
    long ReclaimedBytes,
    DateTimeOffset ProcessedAt,
    string StatusLine,
    string GuidanceLine,
    string? QuarantinePath = null)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
