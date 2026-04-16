namespace AegisTune.Core;

public sealed record DriverAuditExportResult(
    DateTimeOffset ExportedAt,
    string ExportDirectory,
    string JsonPath,
    string MarkdownPath,
    string? HandoffPath = null,
    string? RemediationBundlePath = null,
    string? RemediationPlansDirectory = null)
{
    public string ExportedAtLabel => ExportedAt.ToLocalTime().ToString("g");
}
