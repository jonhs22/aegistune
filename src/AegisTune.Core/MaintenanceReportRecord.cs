namespace AegisTune.Core;

public sealed record MaintenanceReportRecord(
    Guid Id,
    DateTimeOffset GeneratedAt,
    string DeviceName,
    string OperatingSystem,
    int TotalIssueCount,
    IReadOnlyList<ReportModuleSummary> Modules,
    string SummaryFingerprint)
{
    public string GeneratedAtLabel => GeneratedAt.ToLocalTime().ToString("g");

    public string IssueSummaryLabel => TotalIssueCount == 1 ? "1 issue" : $"{TotalIssueCount:N0} issues";
}
