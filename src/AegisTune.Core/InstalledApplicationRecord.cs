namespace AegisTune.Core;

public sealed record InstalledApplicationRecord(
    string DisplayName,
    string DisplayVersion,
    string Publisher,
    InstalledApplicationSource Source,
    string ScopeLabel,
    string RegistryKeyPath,
    string? InstallLocation,
    bool InstallLocationExists,
    string? UninstallCommand,
    string? ResolvedUninstallTargetPath,
    bool UninstallTargetExists,
    long? EstimatedSizeBytes,
    IReadOnlyList<ApplicationResidueRecord>? ResidueEvidence = null)
{
    public string SourceLabel => Source switch
    {
        InstalledApplicationSource.DesktopRegistry => "Desktop app",
        InstalledApplicationSource.Packaged => "Packaged app",
        _ => "Unknown source"
    };

    public string VersionLabel => string.IsNullOrWhiteSpace(DisplayVersion) ? "Version unknown" : DisplayVersion;

    public string PublisherLabel => string.IsNullOrWhiteSpace(Publisher) ? "Publisher unknown" : Publisher;

    public string InstallLocationLabel => string.IsNullOrWhiteSpace(InstallLocation)
        ? "Install location not reported."
        : InstallLocation;

    public string UninstallCommandLabel => string.IsNullOrWhiteSpace(UninstallCommand)
        ? "Uninstall command not reported."
        : UninstallCommand;

    public string UninstallTargetLabel => string.IsNullOrWhiteSpace(ResolvedUninstallTargetPath)
        ? "Uninstall target could not be resolved."
        : ResolvedUninstallTargetPath;

    public string EstimatedSizeLabel => EstimatedSizeBytes is > 0
        ? DataSizeFormatter.FormatBytes(EstimatedSizeBytes.Value)
        : "Size unknown";

    public bool HasBrokenInstallEvidence =>
        Source == InstalledApplicationSource.DesktopRegistry
        && !string.IsNullOrWhiteSpace(InstallLocation)
        && !InstallLocationExists
        && !string.IsNullOrWhiteSpace(ResolvedUninstallTargetPath)
        && !UninstallTargetExists;

    public IReadOnlyList<ApplicationResidueRecord> FilesystemResidue =>
        ResidueEvidence ?? Array.Empty<ApplicationResidueRecord>();

    public bool HasFilesystemResidue => FilesystemResidue.Count > 0;

    public int FilesystemResidueCount => FilesystemResidue.Count;

    public long FilesystemResidueBytes => FilesystemResidue.Sum(entry => entry.SizeBytes);

    public int FilesystemResidueFileCount => FilesystemResidue.Sum(entry => entry.FileCount);

    public ApplicationResidueRecord? PrimaryResidue =>
        FilesystemResidue
            .OrderByDescending(entry => entry.SizeBytes)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    public bool HasPrimaryResiduePath => PrimaryResidue is not null;

    public string PrimaryResiduePathLabel => PrimaryResidue?.Path ?? "No leftover folder path recorded.";

    public string FilesystemResidueSummaryLabel => !HasFilesystemResidue
        ? "No leftover filesystem footprint detected."
        : $"{FilesystemResidueCount:N0} leftover folder(s) currently expose {DataSizeFormatter.FormatBytes(FilesystemResidueBytes)} across {(FilesystemResidueFileCount == 1 ? "1 file" : $"{FilesystemResidueFileCount:N0} files")}.";

    public string FilesystemResiduePreview => !HasFilesystemResidue
        ? "No leftover filesystem footprint detected."
        : string.Join(
            Environment.NewLine,
            FilesystemResidue.Take(4).Select(entry => $"{entry.ScopeLabel}: {entry.Path} ({entry.SizeLabel})"));

    public bool NeedsLeftoverReview =>
        Source == InstalledApplicationSource.DesktopRegistry
        && (HasBrokenInstallEvidence || HasFilesystemResidue && (!UninstallTargetExists || !HasUninstallCommand));

    public string BrokenInstallEvidenceLabel => HasBrokenInstallEvidence
        ? "Broken install evidence detected."
        : "No broken install evidence detected.";

    public bool HasInstallLocation => !string.IsNullOrWhiteSpace(InstallLocation);

    public bool HasUninstallCommand => !string.IsNullOrWhiteSpace(UninstallCommand);

    public bool HasResolvedUninstallTargetPath => !string.IsNullOrWhiteSpace(ResolvedUninstallTargetPath);

    public bool CanRunUninstall =>
        Source == InstalledApplicationSource.DesktopRegistry
        && HasUninstallCommand;

    public bool CanCleanConfirmedResidue => HasFilesystemResidue;
}
