using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class FileDriverAuditExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesPriorityHandoffAndRemediationBundle()
    {
        string exportRoot = Path.Combine(
            Path.GetTempPath(),
            "AegisTune.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            DeviceInventorySnapshot snapshot = new(
                [
                    new DriverDeviceRecord(
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
                        ])
                ],
                DateTimeOffset.Parse("2026-04-15T22:05:00+00:00"));

            FileDriverAuditExportService service = new(exportRoot);
            DriverAuditExportResult result = await service.ExportAsync(snapshot);

            Assert.True(File.Exists(result.JsonPath));
            Assert.True(File.Exists(result.MarkdownPath));
            Assert.True(File.Exists(result.HandoffPath));
            Assert.True(File.Exists(result.RemediationBundlePath));
            Assert.True(Directory.Exists(result.RemediationPlansDirectory));

            string bundle = await File.ReadAllTextAsync(result.RemediationBundlePath!);
            Assert.Contains("# AegisTune Priority Driver Remediation Bundle", bundle);
            Assert.Contains("Remediation source:", bundle);

            string[] remediationFiles = Directory.GetFiles(result.RemediationPlansDirectory!, "*.md");
            Assert.NotEmpty(remediationFiles);
            string firstPlan = await File.ReadAllTextAsync(remediationFiles[0]);
            Assert.Contains("# AegisTune Driver Remediation Plan", firstPlan);
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
