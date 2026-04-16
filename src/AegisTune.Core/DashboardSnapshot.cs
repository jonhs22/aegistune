namespace AegisTune.Core;

public sealed record DashboardSnapshot(
    SystemProfile Profile,
    FirmwareInventorySnapshot Firmware,
    AppSettings Settings,
    IReadOnlyList<ModuleSnapshot> Modules,
    IReadOnlyList<RecentActivity> Activities)
{
    public int TotalIssueCount => Modules.Sum(module => module.IssueCount);

    public int ReadyModuleCount => Modules.Count(module => module.Readiness is not ModuleReadiness.Planned);

    public int ReviewModuleCount => Modules.Count(module => module.RiskLevel is RiskLevel.Review or RiskLevel.Risky);

    public ModuleSnapshot? GetModule(AppSection section) =>
        Modules.FirstOrDefault(module => module.Section == section);
}
