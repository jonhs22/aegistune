using System.Text;
using System.Text.Json;

namespace AegisTune.RepairEngine;

public sealed class FileRepairAdvisoryExportService : IRepairAdvisoryExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _exportRoot;

    public FileRepairAdvisoryExportService(string? exportRoot = null)
    {
        _exportRoot = exportRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "RepairAdvisories");
    }

    public async Task<RepairAdvisoryExportResult> ExportAsync(
        RepairAdvisoryExportRequest advisory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(advisory);

        Directory.CreateDirectory(_exportRoot);

        string slug = $"{SanitizePathPart(advisory.AdvisoryScope)}-{advisory.ObservedAt:yyyyMMdd-HHmmss}";
        string exportDirectory = Path.Combine(_exportRoot, slug);
        Directory.CreateDirectory(exportDirectory);

        string jsonPath = Path.Combine(exportDirectory, "repair-advisory.json");
        string markdownPath = Path.Combine(exportDirectory, "repair-advisory.md");

        var payload = new
        {
            Publisher = "ichiphost",
            Support = "info@ichiphost.gr",
            Creator = "John Papadakis",
            advisory.AdvisoryScope,
            advisory.ObservedAt,
            advisory.StatusLine,
            advisory.ManualInput,
            Candidates = advisory.Candidates,
            OfficialResources = advisory.OfficialResources
        };

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(payload, SerializerOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            RepairAdvisoryDocumentFormatter.BuildMarkdown(advisory),
            cancellationToken);

        return new RepairAdvisoryExportResult(
            DateTimeOffset.Now,
            exportDirectory,
            jsonPath,
            markdownPath);
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
