namespace AegisTune.Core;

public sealed record RepairScanResult(
    IReadOnlyList<RepairCandidateRecord> Candidates,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public int CandidateCount => Candidates.Count;

    public int ReviewCount => Candidates.Count(candidate => candidate.RiskLevel is RiskLevel.Review or RiskLevel.Risky);
}
