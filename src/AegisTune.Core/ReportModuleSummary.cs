namespace AegisTune.Core;

public sealed record ReportModuleSummary(
    AppSection Section,
    string Title,
    string Summary,
    string PrimaryMetric,
    int IssueCount);
