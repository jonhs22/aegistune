namespace AegisTune.Core;

public sealed record DeviceInventorySnapshot(
    IReadOnlyList<DriverDeviceRecord> Devices,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public int TotalDeviceCount => Devices.Count;

    public int NeedsAttentionCount => Devices.Count(device => device.NeedsAttention);

    public int ProblemDeviceCount => Devices.Count(device => device.ProblemCode != 0);

    public int PriorityReviewCount => Devices.Count(device => device.RequiresPriorityReview);

    public int AdvisoryReviewCount => Devices.Count(device => device.NeedsAttention && !device.RequiresPriorityReview);

    public int HealthyCount => Devices.Count(device => !device.NeedsAttention);

    public int GenericProviderReviewCount => Devices.Count(device => device.UsesGenericProviderReview);

    public int UnsignedDriverCount => Devices.Count(device => device.HasSigningConcern);

    public int CriticalClassCount => Devices.Count(device => device.IsCriticalClass);

    public int HighConfidenceMatchCount => Devices.Count(device => device.MatchConfidence == DriverMatchConfidence.High);

    public int MediumConfidenceMatchCount => Devices.Count(device => device.MatchConfidence == DriverMatchConfidence.Medium);

    public int LowConfidenceMatchCount => Devices.Count(device => device.MatchConfidence == DriverMatchConfidence.Low);

    public int UnknownConfidenceMatchCount => Devices.Count(device => device.MatchConfidence == DriverMatchConfidence.Unknown);

    public int HighConfidenceOemCandidateCount => Devices.Count(device => device.IsHighConfidenceOemCandidate);

    public int HardwareBackedReviewCount => Devices.Count(device =>
        device.NeedsAttention
        && device.EvidenceTier == DriverEvidenceTier.HardwareBacked);

    public int CompatibleFallbackReviewCount => Devices.Count(device =>
        device.NeedsAttention
        && device.EvidenceTier == DriverEvidenceTier.CompatibleFallback);

    public int NoIdentifierReviewCount => Devices.Count(device =>
        device.NeedsAttention
        && device.EvidenceTier == DriverEvidenceTier.NoIdentifierEvidence);
}
