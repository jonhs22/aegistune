namespace AegisTune.Core;

public sealed record WindowsHealthSnapshot(
    IReadOnlyList<WindowsHealthEventRecord> CrashEvents,
    IReadOnlyList<WindowsHealthEventRecord> WindowsUpdateEvents,
    IReadOnlyList<ServiceReviewRecord> ServiceCandidates,
    IReadOnlyList<ScheduledTaskReviewRecord> ScheduledTaskCandidates,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public int CrashCount => CrashEvents.Count;

    public int WindowsUpdateIssueCount => WindowsUpdateEvents.Count;

    public int ServiceReviewCount => ServiceCandidates.Count;

    public int ScheduledTaskReviewCount => ScheduledTaskCandidates.Count;

    public int IssueCount => CrashCount + WindowsUpdateIssueCount + ServiceReviewCount + ScheduledTaskReviewCount;

    public string ScannedAtLabel => ScannedAt.ToLocalTime().ToString("g");
}
