namespace AegisTune.Core;

public sealed record ApplicationResidueRecord(
    string Path,
    string ScopeLabel,
    long SizeBytes,
    int FileCount)
{
    public string SizeLabel => DataSizeFormatter.FormatBytes(SizeBytes);

    public string FileCountLabel => FileCount == 1
        ? "1 file"
        : $"{FileCount:N0} files";

    public string SummaryLabel => $"{ScopeLabel} leftover: {SizeLabel} across {FileCountLabel}.";
}
