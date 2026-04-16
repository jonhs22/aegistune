namespace AegisTune.Core;

public sealed record DriverStoreDeviceEvidenceResult(
    string DeviceInstanceId,
    string CommandLine,
    bool QuerySucceeded,
    bool DeviceFound,
    int ExitCode,
    DateTimeOffset CollectedAt,
    string DeviceDescription,
    string DeviceStatus,
    string ReportedDriverName,
    IReadOnlyList<DriverStoreCandidateEvidence> MatchingDrivers,
    string RawOutput,
    string StatusLine,
    string GuidanceLine)
{
    public DriverStoreCandidateEvidence? InstalledDriver =>
        MatchingDrivers.FirstOrDefault(candidate => candidate.IsInstalled)
        ?? MatchingDrivers.FirstOrDefault(candidate =>
            string.Equals(candidate.DriverName, ReportedDriverName, StringComparison.OrdinalIgnoreCase));

    public DriverStoreCandidateEvidence? BestRankedInstalledDriver =>
        MatchingDrivers.FirstOrDefault(candidate => candidate.IsInstalled && candidate.IsBestRanked)
        ?? InstalledDriver;

    public int MatchingDriverCount => MatchingDrivers.Count;

    public int InstalledDriverCount => MatchingDrivers.Count(candidate => candidate.IsInstalled);

    public int OutrankedDriverCount => MatchingDrivers.Count(candidate =>
        !candidate.IsInstalled
        && !string.IsNullOrWhiteSpace(candidate.DriverStatus));

    public string CollectedAtLabel => CollectedAt.ToLocalTime().ToString("g");

    public string DeviceDescriptionLabel => string.IsNullOrWhiteSpace(DeviceDescription) ? "Device description unavailable" : DeviceDescription;

    public string DeviceStatusLabel => string.IsNullOrWhiteSpace(DeviceStatus) ? "Device status unavailable" : DeviceStatus;

    public string ReportedDriverLabel => string.IsNullOrWhiteSpace(ReportedDriverName) ? "Reported installed driver unavailable" : ReportedDriverName;

    public string InstalledDriverSummary => InstalledDriver?.SummaryLine ?? "No installed driver candidate was identified in the driver-store evidence.";

    public string BestRankedInstalledDriverSummary => BestRankedInstalledDriver?.SummaryLine ?? "No best-ranked installed driver candidate was identified in the driver-store evidence.";

    public string MatchingDriverCountLabel => $"{MatchingDriverCount:N0} matching driver{(MatchingDriverCount == 1 ? string.Empty : "s")}";

    public string OutrankedDriverCountLabel => $"{OutrankedDriverCount:N0} outranked candidate{(OutrankedDriverCount == 1 ? string.Empty : "s")}";

    public string MatchingDriversPreview
    {
        get
        {
            if (MatchingDrivers.Count == 0)
            {
                return "No matching driver packages were reported by pnputil for this device.";
            }

            IEnumerable<DriverStoreCandidateEvidence> previewDrivers = MatchingDrivers
                .DistinctBy(candidate => $"{candidate.DriverName}|{candidate.DriverStatus}|{candidate.DriverRank}")
                .Take(5);

            return string.Join(
                Environment.NewLine,
                previewDrivers.Select((candidate, index) => $"{index + 1}. {candidate.SummaryLine}"));
        }
    }
}
