using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class FileDriverRemediationExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesDriverPlanArtifacts()
    {
        string exportRoot = Path.Combine(
            Path.GetTempPath(),
            "AegisTune.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            DriverDeviceRecord device = new(
                "Intel Wi-Fi 6 AX201",
                "Net",
                "Intel",
                "Intel",
                "23.40.0.4",
                "Error",
                10,
                "PCI\\VEN_8086&DEV_43F0",
                "netwtw14.inf",
                DateTimeOffset.Parse("2026-04-10"),
                IsSigned: true,
                SignerName: "Microsoft Windows Hardware Compatibility Publisher",
                ClassGuid: "{4d36e972-e325-11ce-bfc1-08002be10318}",
                ServiceName: "Netwtw14",
                IsPresent: true,
                HardwareIds:
                [
                    "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
                ]);
            DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);
            FileDriverRemediationExportService service = new(exportRoot);

            DriverRemediationExportResult result = await service.ExportAsync(device, plan);

            Assert.True(File.Exists(result.JsonPath));
            Assert.True(File.Exists(result.MarkdownPath));

            string markdown = await File.ReadAllTextAsync(result.MarkdownPath);
            Assert.Contains("# AegisTune Driver Remediation Plan", markdown);
            Assert.Contains("Intel Wi-Fi 6 AX201", markdown);
            Assert.Contains("Verification checklist", markdown);
        }
        finally
        {
            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }
}
