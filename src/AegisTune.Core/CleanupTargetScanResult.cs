namespace AegisTune.Core;

public sealed record CleanupTargetScanResult(
    string Title,
    string Description,
    string LocationSummary,
    CleanupTargetStatus Status,
    bool EnabledByDefault,
    int FileCount,
    long ReclaimableBytes,
    string? Notes = null,
    bool SupportsExecution = false)
{
    public bool HasFindings => Status == CleanupTargetStatus.Ready && (FileCount > 0 || ReclaimableBytes > 0);

    public bool CanExecute => SupportsExecution && HasFindings;

    public string StatusLabel => Status switch
    {
        CleanupTargetStatus.Ready => "Ready",
        CleanupTargetStatus.Empty => "Empty",
        CleanupTargetStatus.Skipped => "Skipped",
        CleanupTargetStatus.Error => "Error",
        _ => "Unknown"
    };

    public string FormattedReclaimableSize => DataSizeFormatter.FormatBytes(ReclaimableBytes);

    public string FormattedFileCount => FileCount == 1 ? "1 file" : $"{FileCount:N0} files";

    public string DefaultSelectionLabel => EnabledByDefault ? "Included by default" : "Opt-in target";

    public string ExecutionModeLabel => SupportsExecution ? "Guided cleanup available" : "Preview only";
}
