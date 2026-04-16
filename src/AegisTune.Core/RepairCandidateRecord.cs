namespace AegisTune.Core;

public sealed record RepairCandidateRecord(
    string Title,
    string Category,
    RiskLevel RiskLevel,
    bool RequiresAdministrator,
    string EvidenceSummary,
    string ProposedAction,
    string SourceLocation,
    string? RelatedApplicationName = null,
    string? ApplicationPath = null,
    bool ApplicationPathExists = false,
    string? InstallLocation = null,
    bool InstallLocationExists = false,
    string? UninstallCommand = null,
    string? UninstallTargetPath = null,
    bool UninstallTargetExists = false,
    string? ResidueFolderPath = null,
    bool ResidueFolderExists = false,
    string? ResidueSummary = null,
    string? OfficialResourceTitle = null,
    string? OfficialResourceLabel = null,
    Uri? OfficialResourceUri = null,
    RegistryRepairPackKind RegistryRepairPackKind = RegistryRepairPackKind.None,
    string? RegistryPath = null,
    string? RegistryValueName = null,
    int? RegistryDwordValue = null,
    string? RepairActionLabel = null)
{
    public string RiskLabel => RiskLevel switch
    {
        RiskLevel.Safe => "Safe",
        RiskLevel.Review => "Review",
        RiskLevel.Risky => "Risky",
        _ => "Unknown"
    };

    public string AdminRequirementLabel => RequiresAdministrator ? "Admin required" : "No elevation";

    public bool HasRelatedApplication => !string.IsNullOrWhiteSpace(RelatedApplicationName);

    public string RelatedApplicationLabel => string.IsNullOrWhiteSpace(RelatedApplicationName)
        ? "No matched installed application"
        : RelatedApplicationName!;

    public bool HasApplicationPath => !string.IsNullOrWhiteSpace(ApplicationPath);

    public string ApplicationPathLabel => string.IsNullOrWhiteSpace(ApplicationPath)
        ? "Application path not recorded."
        : ApplicationPath!;

    public bool HasInstallLocation => !string.IsNullOrWhiteSpace(InstallLocation);

    public string InstallLocationLabel => string.IsNullOrWhiteSpace(InstallLocation)
        ? "Install location not recorded."
        : InstallLocation!;

    public bool HasUninstallCommand => !string.IsNullOrWhiteSpace(UninstallCommand);

    public string UninstallCommandLabel => string.IsNullOrWhiteSpace(UninstallCommand)
        ? "Uninstall command not recorded."
        : UninstallCommand!;

    public bool HasUninstallTargetPath => !string.IsNullOrWhiteSpace(UninstallTargetPath);

    public string UninstallTargetLabel => string.IsNullOrWhiteSpace(UninstallTargetPath)
        ? "Uninstall target path not recorded."
        : UninstallTargetPath!;

    public bool HasResidueFolderPath => !string.IsNullOrWhiteSpace(ResidueFolderPath);

    public string ResidueFolderPathLabel => string.IsNullOrWhiteSpace(ResidueFolderPath)
        ? "No leftover folder path recorded."
        : ResidueFolderPath!;

    public bool HasResidueSummary => !string.IsNullOrWhiteSpace(ResidueSummary);

    public string ResidueSummaryLabel => string.IsNullOrWhiteSpace(ResidueSummary)
        ? "No leftover filesystem footprint recorded."
        : ResidueSummary!;

    public bool HasOfficialResource => OfficialResourceUri is not null;

    public string OfficialResourceTitleLabel => string.IsNullOrWhiteSpace(OfficialResourceTitle)
        ? "No candidate-specific official repair link"
        : OfficialResourceTitle!;

    public string OfficialResourceLabelText => string.IsNullOrWhiteSpace(OfficialResourceLabel)
        ? "No candidate-specific official repair link"
        : OfficialResourceLabel!;

    public bool CanOpenApplicationReviewFlow => HasRelatedApplication;

    public string ApplicationReviewActionLabel => HasUninstallCommand || HasResidueFolderPath
        ? "Open app cleanup flow"
        : "Open app review";

    public bool CanExecuteInAppRepairPack =>
        RegistryRepairPackKind != RegistryRepairPackKind.None
        && !string.IsNullOrWhiteSpace(RegistryPath);

    public string RegistryPathLabel => string.IsNullOrWhiteSpace(RegistryPath)
        ? "No registry target recorded."
        : RegistryPath!;

    public string RepairActionLabelText => string.IsNullOrWhiteSpace(RepairActionLabel)
        ? RegistryRepairPackKind switch
        {
            RegistryRepairPackKind.RemoveRegistryKey => "Back up + remove registry entry",
            RegistryRepairPackKind.SetDwordValue => "Back up + apply registry fix",
            _ => "No in-app repair action"
        }
        : RepairActionLabel!;
}
