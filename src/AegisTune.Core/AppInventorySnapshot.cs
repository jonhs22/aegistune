namespace AegisTune.Core;

public sealed record AppInventorySnapshot(
    IReadOnlyList<InstalledApplicationRecord> Applications,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public int ApplicationCount => Applications.Count;

    public int DesktopApplicationCount => Applications.Count(app => app.Source == InstalledApplicationSource.DesktopRegistry);

    public int PackagedApplicationCount => Applications.Count(app => app.Source == InstalledApplicationSource.Packaged);

    public int BrokenInstallEvidenceCount => Applications.Count(app => app.HasBrokenInstallEvidence);

    public int LeftoverReviewCandidateCount => Applications.Count(app => app.NeedsLeftoverReview);

    public int FilesystemResidueCandidateCount => Applications.Count(app => app.HasFilesystemResidue);
}
