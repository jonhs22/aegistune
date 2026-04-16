namespace AegisTune.RepairEngine;

public sealed record RepairAdvisoryExportResult(
    DateTimeOffset ExportedAt,
    string ExportDirectory,
    string JsonPath,
    string MarkdownPath)
{
    public string ExportedAtLabel => ExportedAt.ToLocalTime().ToString("g");
}
