namespace AegisTune.Core;

public sealed record DriverRepositoryCandidate(
    string InfPath,
    string RepositoryRoot,
    string Provider,
    string DriverClass,
    string DriverVersion,
    string? CatalogFile,
    DriverRepositoryMatchKind MatchKind,
    IReadOnlyList<string> MatchedIdentifiers)
{
    public string FileName => Path.GetFileName(InfPath);

    public string DirectoryPath => Path.GetDirectoryName(InfPath) ?? RepositoryRoot;

    public string ProviderLabel => string.IsNullOrWhiteSpace(Provider) ? "Provider unknown" : Provider;

    public string ClassLabel => string.IsNullOrWhiteSpace(DriverClass) ? "Class unknown" : DriverClass;

    public string VersionLabel => string.IsNullOrWhiteSpace(DriverVersion) ? "Version unknown" : DriverVersion;

    public string CatalogLabel => string.IsNullOrWhiteSpace(CatalogFile) ? "Catalog unknown" : CatalogFile;

    public int MatchPriority => (int)MatchKind;

    public string MatchKindLabel => MatchKind switch
    {
        DriverRepositoryMatchKind.ExactHardwareId => "Exact hardware ID match",
        DriverRepositoryMatchKind.GenericHardwareId => "Generic hardware ID match",
        DriverRepositoryMatchKind.ExactCompatibleId => "Exact compatible ID match",
        _ => "Generic compatible ID match"
    };

    public string MatchDetail => MatchKind switch
    {
        DriverRepositoryMatchKind.ExactHardwareId => "The INF exposes an exact hardware ID already reported by Windows for this device.",
        DriverRepositoryMatchKind.GenericHardwareId => "The INF exposes a less specific hardware ID family that still aligns with the current device hardware evidence.",
        DriverRepositoryMatchKind.ExactCompatibleId => "The INF lines up with a compatible ID reported by Windows. Keep this on technician review rather than treating it as a trusted OEM match.",
        _ => "The INF lines up only with a generic compatible ID family. Treat it as fallback evidence, not as a high-trust automatic package."
    };

    public string SummaryLine => $"{MatchKindLabel} • {ProviderLabel} • {VersionLabel}";

    public string MatchedIdentifierCountLabel => MatchedIdentifiers.Count == 0
        ? "No matched identifiers captured"
        : $"{MatchedIdentifiers.Count:N0} matched identifier{(MatchedIdentifiers.Count == 1 ? string.Empty : "s")}";

    public string MatchedIdentifiersPreview => MatchedIdentifiers.Count == 0
        ? "No matched identifiers captured for this INF candidate."
        : string.Join(Environment.NewLine, MatchedIdentifiers.Take(5));
}
