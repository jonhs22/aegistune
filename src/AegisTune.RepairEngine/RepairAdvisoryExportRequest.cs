using AegisTune.Core;

namespace AegisTune.RepairEngine;

public sealed record RepairAdvisoryExportRequest(
    string AdvisoryScope,
    DateTimeOffset ObservedAt,
    string StatusLine,
    IReadOnlyList<RepairCandidateRecord> Candidates,
    IReadOnlyList<RepairResourceLink> OfficialResources,
    string? ManualInput = null)
{
    public int CandidateCount => Candidates.Count;
}
