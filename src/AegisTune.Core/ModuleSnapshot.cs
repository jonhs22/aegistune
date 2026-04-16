namespace AegisTune.Core;

public sealed record ModuleSnapshot(
    AppSection Section,
    string Title,
    string Subtitle,
    RiskLevel RiskLevel,
    ModuleReadiness Readiness,
    int IssueCount,
    string PrimaryMetric,
    string PrimaryMetricLabel,
    string StatusLine,
    bool RequiresAdministrator,
    IReadOnlyList<ModuleAction> Actions)
{
    public string RiskLabel => RiskLevel switch
    {
        RiskLevel.Safe => "Safe",
        RiskLevel.Review => "Review",
        RiskLevel.Risky => "Risky",
        _ => "Unknown"
    };

    public string ReadinessLabel => Readiness switch
    {
        ModuleReadiness.Operational => "Operational",
        ModuleReadiness.Preview => "Preview",
        ModuleReadiness.Planned => "Planned",
        _ => "Unknown"
    };

    public string AdminRequirementLabel => RequiresAdministrator ? "Admin required" : "No elevation";

    public IReadOnlyList<ModuleAction> SuggestedActions => Actions.Take(2).ToArray();

    public string OpenModuleLabel => $"Open {Title}";
}
