namespace AegisTune.Core;

public sealed record DriverDepotScanResult(
    IReadOnlyList<string> ConfiguredRoots,
    IReadOnlyList<string> ActiveRoots,
    int PackageCount,
    IReadOnlyDictionary<string, IReadOnlyList<DriverRepositoryCandidate>> CandidatesByInstanceId,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public int ConfiguredRootCount => ConfiguredRoots.Count;

    public int ActiveRootCount => ActiveRoots.Count;

    public int MissingRootCount => Math.Max(0, ConfiguredRootCount - ActiveRootCount);

    public string ScannedAtLabel => ScannedAt.ToLocalTime().ToString("g");

    public string StatusLine
    {
        get
        {
            if (ConfiguredRootCount == 0)
            {
                return "No vetted local driver repositories are configured yet.";
            }

            if (ActiveRootCount == 0)
            {
                return "Configured driver repositories are not currently accessible.";
            }

            if (!string.IsNullOrWhiteSpace(WarningMessage))
            {
                return WarningMessage;
            }

            return $"Scanned {PackageCount:N0} INF package{(PackageCount == 1 ? string.Empty : "s")} across {ActiveRootCount:N0} active repository root{(ActiveRootCount == 1 ? string.Empty : "s")}.";
        }
    }

    public string RootSummaryLabel
    {
        get
        {
            if (ConfiguredRootCount == 0)
            {
                return "Add one vetted OEM or technician-curated driver repository path in Settings to enable local driver installs.";
            }

            if (MissingRootCount == 0)
            {
                return $"{ConfiguredRootCount:N0} configured repository root{(ConfiguredRootCount == 1 ? string.Empty : "s")} are currently available.";
            }

            return $"{ActiveRootCount:N0} of {ConfiguredRootCount:N0} configured repository root{(ConfiguredRootCount == 1 ? string.Empty : "s")} are currently available.";
        }
    }

    public IReadOnlyList<DriverRepositoryCandidate> GetCandidates(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return Array.Empty<DriverRepositoryCandidate>();
        }

        return CandidatesByInstanceId.TryGetValue(instanceId, out IReadOnlyList<DriverRepositoryCandidate>? candidates)
            ? candidates
            : Array.Empty<DriverRepositoryCandidate>();
    }
}
