using AegisTune.Core;
using AegisTune.RepairEngine;

namespace AegisTune.Core.Tests;

public sealed class FileRepairAdvisoryExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesJsonAndMarkdownArtifacts()
    {
        string exportRoot = Path.Combine(
            Path.GetTempPath(),
            "AegisTune.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            FileRepairAdvisoryExportService service = new(exportRoot);
            RepairAdvisoryExportRequest advisory = new(
                "Manual dependency advisory",
                DateTimeOffset.Parse("2026-04-15T21:45:00+00:00"),
                "1 official remediation candidate was generated from the pasted error text.",
                [
                    new RepairCandidateRecord(
                        "Dependency repair review: Adobe Photoshop 2026",
                        "Dependency",
                        RiskLevel.Review,
                        RequiresAdministrator: true,
                        "MSVCP140.dll was reported as missing by the pasted launch error.",
                        "Repair Microsoft Visual C++ Redistributable first, then run the official Adobe repair path.",
                        @"C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe",
                        "Adobe Photoshop 2026",
                        @"C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe",
                        false,
                        @"C:\Program Files\Adobe\Adobe Photoshop 2026",
                        true,
                        "\"C:\\Program Files\\Adobe\\Adobe Photoshop 2026\\uninstall.exe\"",
                        @"C:\Program Files\Adobe\Adobe Photoshop 2026\uninstall.exe",
                        true,
                        OfficialResourceTitle: "Latest supported Visual C++ Redistributable",
                        OfficialResourceLabel: "Open Microsoft VC++ runtime page",
                        OfficialResourceUri: new Uri("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170"))
                ],
                RepairResourceCatalog.All,
                """
                The program can't start because MSVCP140.dll is missing from your computer.
                C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe
                """);

            RepairAdvisoryExportResult result = await service.ExportAsync(advisory);

            Assert.True(File.Exists(result.JsonPath));
            Assert.True(File.Exists(result.MarkdownPath));

            string markdown = await File.ReadAllTextAsync(result.MarkdownPath);
            Assert.Contains("# AegisTune Repair Advisory", markdown);
            Assert.Contains("Adobe Photoshop 2026", markdown);
            Assert.Contains("Official repair links", markdown);
            Assert.Contains("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170", markdown);
            Assert.Contains("App path:", markdown);
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
