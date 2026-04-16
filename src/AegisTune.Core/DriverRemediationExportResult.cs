namespace AegisTune.Core;

public sealed record DriverRemediationExportResult(
    DateTimeOffset ExportedAt,
    string ExportDirectory,
    string JsonPath,
    string MarkdownPath)
{
    public string ExportedAtLabel => ExportedAt.ToLocalTime().ToString("g");
}
