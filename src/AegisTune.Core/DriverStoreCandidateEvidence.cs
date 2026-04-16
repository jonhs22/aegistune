namespace AegisTune.Core;

public sealed record DriverStoreCandidateEvidence(
    string DriverName,
    string? OriginalName,
    string? ProviderName,
    string? DriverVersion,
    string? SignerName,
    string? MatchingDeviceId,
    string? DriverRank,
    string? DriverStatus)
{
    private string NormalizedStatus => string.IsNullOrWhiteSpace(DriverStatus)
        ? string.Empty
        : DriverStatus.Replace("/", " ", StringComparison.Ordinal);

    public bool IsInstalled =>
        NormalizedStatus.Contains("installed", StringComparison.OrdinalIgnoreCase);

    public bool IsBestRanked =>
        NormalizedStatus.Contains("best ranked", StringComparison.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(OriginalName)
        ? DriverName
        : $"{DriverName} ({OriginalName})";

    public string ProviderLabel => string.IsNullOrWhiteSpace(ProviderName) ? "Provider unknown" : ProviderName;

    public string VersionLabel => string.IsNullOrWhiteSpace(DriverVersion) ? "Version unknown" : DriverVersion;

    public string SignerLabel => string.IsNullOrWhiteSpace(SignerName) ? "Signer unknown" : SignerName;

    public string RankLabel => string.IsNullOrWhiteSpace(DriverRank) ? "Rank unavailable" : DriverRank;

    public string StatusLabel => string.IsNullOrWhiteSpace(DriverStatus) ? "Driver status unavailable" : DriverStatus;

    public string MatchingDeviceIdLabel => string.IsNullOrWhiteSpace(MatchingDeviceId) ? "Matching device ID unavailable" : MatchingDeviceId;

    public string SummaryLine => $"{DisplayName} • {ProviderLabel} • {VersionLabel} • {StatusLabel} • {RankLabel}";
}
