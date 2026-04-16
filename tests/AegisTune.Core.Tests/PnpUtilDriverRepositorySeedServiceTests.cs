using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class PnpUtilDriverRepositorySeedServiceTests
{
    [Fact]
    public async Task ExportInstalledPackageAsync_InDryRun_DoesNotInvokeRunner()
    {
        string targetRoot = CreateTempDirectory();

        try
        {
            FakeDriverCommandRunner runner = new(0);
            PnpUtilDriverRepositorySeedService service = new(runner);

            DriverRepositorySeedResult result = await service.ExportInstalledPackageAsync(
                CreateExportableDevice(),
                targetRoot,
                dryRunEnabled: true);

            Assert.True(result.WasDryRun);
            Assert.True(result.Succeeded);
            Assert.Null(runner.LastArguments);
            Assert.Contains("pnputil.exe /export-driver", result.CommandLine, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(targetRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExportInstalledPackageAsync_InLiveMode_UsesPnPUtilExportCommand()
    {
        string targetRoot = CreateTempDirectory();

        try
        {
            FakeDriverCommandRunner runner = new(0);
            PnpUtilDriverRepositorySeedService service = new(runner);
            DriverDeviceRecord device = CreateExportableDevice();

            DriverRepositorySeedResult result = await service.ExportInstalledPackageAsync(
                device,
                targetRoot,
                dryRunEnabled: false);

            Assert.False(result.WasDryRun);
            Assert.True(result.Succeeded);
            Assert.Equal("pnputil.exe", runner.LastFileName);
            Assert.StartsWith($"/export-driver \"{device.InfName}\" ", runner.LastArguments, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(targetRoot, result.ExportDirectory, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(targetRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExportInstalledPackageAsync_RejectsInboxInfPackages()
    {
        string targetRoot = CreateTempDirectory();

        try
        {
            FakeDriverCommandRunner runner = new(0);
            PnpUtilDriverRepositorySeedService service = new(runner);
            DriverDeviceRecord device = CreateExportableDevice() with { InfName = "netwtw10.inf" };

            DriverRepositorySeedResult result = await service.ExportInstalledPackageAsync(
                device,
                targetRoot,
                dryRunEnabled: false);

            Assert.False(result.Succeeded);
            Assert.Null(runner.LastArguments);
            Assert.Contains("not an exportable third-party OEM driver package", result.StatusLine, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(targetRoot, recursive: true);
        }
    }

    private static DriverDeviceRecord CreateExportableDevice() =>
        new(
            "Contoso Wi-Fi",
            "Net",
            "Contoso",
            "Contoso",
            "1.0.0.0",
            "OK",
            0,
            "PCI\\VEN_1234&DEV_5678",
            InfName: "oem42.inf",
            HardwareIds:
            [
                "PCI\\VEN_1234&DEV_5678&SUBSYS_00011234"
            ]);

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AegisTune.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeDriverCommandRunner : IDriverCommandRunner
    {
        private readonly int _exitCode;

        public FakeDriverCommandRunner(int exitCode)
        {
            _exitCode = exitCode;
        }

        public string? LastFileName { get; private set; }

        public string? LastArguments { get; private set; }

        public Task<int> RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            return Task.FromResult(_exitCode);
        }
    }
}
