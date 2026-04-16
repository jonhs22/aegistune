using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class LocalDriverDepotServiceTests
{
    [Fact]
    public async Task ScanAsync_PrefersExactHardwareMatchesOverGenericFallbacks()
    {
        string repositoryRoot = CreateTempDirectory();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(repositoryRoot, "exact.inf"),
                """
                [Version]
                Signature="$Windows NT$"
                Class=Net
                Provider=%Vendor%
                DriverVer=01/12/2026,3.2.1.0
                CatalogFile=exact.cat

                [Strings]
                Vendor="Contoso"
                DeviceName="Contoso Wi-Fi"

                [Contoso.NTamd64]
                %DeviceName%=Install,PCI\VEN_1234&DEV_5678&SUBSYS_00011234
                """);

            await File.WriteAllTextAsync(
                Path.Combine(repositoryRoot, "fallback.inf"),
                """
                [Version]
                Signature="$Windows NT$"
                Class=Net
                Provider="Contoso"
                DriverVer=01/10/2026,3.1.0.0

                [Contoso.NTamd64]
                %DeviceName%=Install,PCI\VEN_1234&DEV_5678
                """);

            DriverDeviceRecord device = new(
                "Contoso Wi-Fi",
                "Net",
                "Contoso",
                "Microsoft",
                "1.0.0.0",
                "Error",
                28,
                "PCI\\VEN_1234&DEV_5678&SUBSYS_00011234",
                HardwareIds:
                [
                    "PCI\\VEN_1234&DEV_5678&SUBSYS_00011234"
                ]);

            LocalDriverDepotService service = new();
            DriverDepotScanResult result = await service.ScanAsync([repositoryRoot], [device]);
            IReadOnlyList<DriverRepositoryCandidate> candidates = result.GetCandidates(device.InstanceId);

            Assert.Equal(2, result.PackageCount);
            Assert.Equal(2, candidates.Count);
            Assert.Equal(DriverRepositoryMatchKind.ExactHardwareId, candidates[0].MatchKind);
            Assert.Equal("exact.inf", candidates[0].FileName);
            Assert.Equal(DriverRepositoryMatchKind.GenericHardwareId, candidates[1].MatchKind);
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_UsesCompatibleFallbackWhenHardwareIdsAreMissing()
    {
        string repositoryRoot = CreateTempDirectory();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(repositoryRoot, "fallback.inf"),
                """
                [Version]
                Signature="$Windows NT$"
                Class=Bluetooth
                Provider="Fabrikam"
                DriverVer=02/22/2026,5.0.0.0

                [Fabrikam.NTamd64]
                %BtDevice%=Install,USB\Class_E0
                """);

            DriverDeviceRecord device = new(
                "Bluetooth Radio",
                "Bluetooth",
                "Fabrikam",
                "",
                "",
                "Unknown",
                0,
                "USB\\VID_0001&PID_0002",
                CompatibleIds:
                [
                    "USB\\Class_E0"
                ]);

            LocalDriverDepotService service = new();
            DriverDepotScanResult result = await service.ScanAsync([repositoryRoot], [device]);
            DriverRepositoryCandidate candidate = Assert.Single(result.GetCandidates(device.InstanceId));

            Assert.Equal(DriverRepositoryMatchKind.ExactCompatibleId, candidate.MatchKind);
            Assert.Contains("Compatible", candidate.MatchKindLabel, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AegisTune.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        return directory;
    }
}
