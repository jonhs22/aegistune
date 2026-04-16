namespace AegisTune.Core;

public sealed record CleanupTargetExecutionResult(
    string Title,
    bool Succeeded,
    bool Skipped,
    int DeletedFileCount,
    long ReclaimedBytes,
    string Message)
{
    public string DeletedFileCountLabel => DeletedFileCount == 1 ? "1 file" : $"{DeletedFileCount:N0} files";

    public string ReclaimedBytesLabel => DataSizeFormatter.FormatBytes(ReclaimedBytes);

    public string OutcomeLabel => Skipped
        ? "Skipped"
        : Succeeded
            ? "Completed"
            : "Failed";
}
