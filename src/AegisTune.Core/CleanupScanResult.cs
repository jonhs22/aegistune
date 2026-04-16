namespace AegisTune.Core;

public sealed record CleanupScanResult(
    IReadOnlyList<CleanupTargetScanResult> Targets,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public long TotalBytes => Targets.Where(target => target.HasFindings).Sum(target => target.ReclaimableBytes);

    public int TotalFileCount => Targets.Where(target => target.HasFindings).Sum(target => target.FileCount);

    public int ActionableTargetCount => Targets.Count(target => target.HasFindings);

    public string TotalBytesLabel => DataSizeFormatter.FormatBytes(TotalBytes);

    public string TotalFileCountLabel => TotalFileCount == 1 ? "1 file" : $"{TotalFileCount:N0} files";
}
