namespace AegisTune.Core;

public sealed record DependencyRepairSignal(
    string DependencyName,
    string EvidenceSource,
    string EvidenceMessage,
    DateTimeOffset ObservedAt,
    string? ApplicationName = null,
    string? ApplicationPath = null);
