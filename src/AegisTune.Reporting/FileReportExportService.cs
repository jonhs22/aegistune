using System.Text;
using System.Text.Json;
using AegisTune.Core;

namespace AegisTune.Reporting;

public sealed class FileReportExportService : IReportExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _exportRoot;

    public FileReportExportService(string? exportRoot = null)
    {
        _exportRoot = exportRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "Exports");
    }

    public async Task<ReportExportResult> ExportAsync(
        MaintenanceReportRecord report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        Directory.CreateDirectory(_exportRoot);

        string slug = $"{SanitizePathPart(report.DeviceName)}-{report.GeneratedAt:yyyyMMdd-HHmmss}";
        string exportDirectory = Path.Combine(_exportRoot, slug);
        Directory.CreateDirectory(exportDirectory);

        string jsonPath = Path.Combine(exportDirectory, "maintenance-report.json");
        string markdownPath = Path.Combine(exportDirectory, "maintenance-report.md");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, SerializerOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            BuildMarkdown(report),
            cancellationToken);

        return new ReportExportResult(
            DateTimeOffset.Now,
            exportDirectory,
            jsonPath,
            markdownPath);
    }

    private static string BuildMarkdown(MaintenanceReportRecord report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# AegisTune Maintenance Report");
        builder.AppendLine();
        builder.AppendLine("- Publisher: ichiphost");
        builder.AppendLine("- Support: info@ichiphost.gr");
        builder.AppendLine("- Creator: John Papadakis");
        builder.AppendLine($"- Generated: {report.GeneratedAt.ToLocalTime():f}");
        builder.AppendLine($"- Device: {report.DeviceName}");
        builder.AppendLine($"- Operating system: {report.OperatingSystem}");
        builder.AppendLine($"- Total issues: {report.TotalIssueCount:N0}");
        builder.AppendLine();
        builder.AppendLine("## Modules");
        builder.AppendLine();

        foreach (ReportModuleSummary module in report.Modules)
        {
            builder.AppendLine($"### {module.Title}");
            builder.AppendLine($"- Metric: {module.PrimaryMetric}");
            builder.AppendLine($"- Issue count: {module.IssueCount:N0}");
            builder.AppendLine($"- Summary: {module.Summary}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string SanitizePathPart(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString().Trim().Trim('-');
    }
}
