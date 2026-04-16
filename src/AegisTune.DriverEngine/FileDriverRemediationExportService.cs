using System.Text;
using System.Text.Json;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class FileDriverRemediationExportService : IDriverRemediationExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _exportRoot;

    public FileDriverRemediationExportService(string? exportRoot = null)
    {
        _exportRoot = exportRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "DriverPlans");
    }

    public async Task<DriverRemediationExportResult> ExportAsync(
        DriverDeviceRecord device,
        DriverRemediationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(plan);

        Directory.CreateDirectory(_exportRoot);

        string slug = $"{SanitizePathPart(device.FriendlyName)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        string exportDirectory = Path.Combine(_exportRoot, slug);
        Directory.CreateDirectory(exportDirectory);

        string jsonPath = Path.Combine(exportDirectory, "driver-remediation-plan.json");
        string markdownPath = Path.Combine(exportDirectory, "driver-remediation-plan.md");

        var payload = new
        {
            Publisher = "ichiphost",
            Support = "info@ichiphost.gr",
            Creator = "John Papadakis",
            Device = device,
            Plan = plan
        };

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(payload, SerializerOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            DriverRemediationDocumentFormatter.BuildMarkdown(device, plan),
            cancellationToken);

        return new DriverRemediationExportResult(
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
