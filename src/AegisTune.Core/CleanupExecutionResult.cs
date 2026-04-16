namespace AegisTune.Core;

public sealed record CleanupExecutionResult(
    DateTimeOffset ProcessedAt,
    IReadOnlyList<CleanupTargetExecutionResult> Targets)
{
    public int RequestedTargetCount => Targets.Count;

    public int SuccessfulTargetCount => Targets.Count(target => target.Succeeded && !target.Skipped);

    public int SkippedTargetCount => Targets.Count(target => target.Skipped);

    public int FailedTargetCount => Targets.Count(target => !target.Succeeded && !target.Skipped);

    public int DeletedFileCount => Targets.Sum(target => target.DeletedFileCount);

    public long ReclaimedBytes => Targets.Sum(target => target.ReclaimedBytes);

    public string DeletedFileCountLabel => DeletedFileCount == 1 ? "1 file" : $"{DeletedFileCount:N0} files";

    public string ReclaimedBytesLabel => DataSizeFormatter.FormatBytes(ReclaimedBytes);

    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");

    public string SummaryLabel => RequestedTargetCount switch
    {
        0 => "No cleanup targets were selected.",
        _ when FailedTargetCount == 0 && SuccessfulTargetCount == 0 =>
            $"Skipped {SkippedTargetCount:N0} cleanup target(s).",
        _ when FailedTargetCount == 0 =>
            $"Processed {SuccessfulTargetCount:N0} cleanup target(s) and reclaimed {ReclaimedBytesLabel} across {DeletedFileCountLabel}.",
        _ =>
            $"Processed {SuccessfulTargetCount:N0} cleanup target(s) with {FailedTargetCount:N0} failure(s) and reclaimed {ReclaimedBytesLabel} across {DeletedFileCountLabel}."
    };
}
