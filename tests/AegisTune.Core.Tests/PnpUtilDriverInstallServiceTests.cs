using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class PnpUtilDriverInstallServiceTests
{
    [Fact]
    public async Task InstallAsync_InDryRun_DoesNotInvokeRunner()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string infPath = Path.Combine(tempDirectory, "device.inf");
            await File.WriteAllTextAsync(infPath, "placeholder");

            FakeDriverCommandRunner runner = new(0);
            FakeUndoJournalStore undoJournalStore = new();
            PnpUtilDriverInstallService service = new(
                runner,
                new FakeRiskyChangePreflightService(
                    new RiskyChangePreflightResult(
                        true,
                        false,
                        false,
                        true,
                        DateTimeOffset.Now,
                        "Preview mode is active.",
                        "Safe to continue.")),
                undoJournalStore);
            DriverRepositoryCandidate candidate = new(
                infPath,
                tempDirectory,
                "Contoso",
                "Net",
                "1.0.0.0",
                "device.cat",
                DriverRepositoryMatchKind.ExactHardwareId,
                ["PCI\\VEN_1234&DEV_5678"]);

            DriverInstallExecutionResult result = await service.InstallAsync(
                CreateDevice(),
                candidate,
                dryRunEnabled: true);

            Assert.True(result.WasDryRun);
            Assert.True(result.Succeeded);
            Assert.Null(runner.LastArguments);
            Assert.Empty(undoJournalStore.Entries);
            Assert.Contains("pnputil.exe /add-driver", result.CommandLine, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_InLiveMode_UsesPnPUtilCommand()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string infPath = Path.Combine(tempDirectory, "device.inf");
            await File.WriteAllTextAsync(infPath, "placeholder");

            FakeDriverCommandRunner runner = new(0);
            FakeUndoJournalStore undoJournalStore = new();
            PnpUtilDriverInstallService service = new(
                runner,
                new FakeRiskyChangePreflightService(
                    new RiskyChangePreflightResult(
                        true,
                        true,
                        false,
                        false,
                        DateTimeOffset.Now,
                        "Created a Windows restore point.",
                        "Safe to continue.")),
                undoJournalStore);
            DriverRepositoryCandidate candidate = new(
                infPath,
                tempDirectory,
                "Contoso",
                "Net",
                "1.0.0.0",
                "device.cat",
                DriverRepositoryMatchKind.ExactHardwareId,
                ["PCI\\VEN_1234&DEV_5678"]);

            DriverInstallExecutionResult result = await service.InstallAsync(
                CreateDevice(),
                candidate,
                dryRunEnabled: false);

            Assert.False(result.WasDryRun);
            Assert.True(result.Succeeded);
            Assert.Equal("pnputil.exe", runner.LastFileName);
            Assert.Equal($"/add-driver \"{infPath}\" /install", runner.LastArguments);
            Assert.Equal(0, result.ExitCode);
            Assert.Single(undoJournalStore.Entries);
            Assert.Equal(UndoJournalEntryKind.DriverInstall, undoJournalStore.Entries[0].Kind);
            Assert.Equal($"INF path: {infPath}", undoJournalStore.Entries[0].ArtifactLabel);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_WhenPreflightBlocks_DoesNotInvokeRunner()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string infPath = Path.Combine(tempDirectory, "device.inf");
            await File.WriteAllTextAsync(infPath, "placeholder");

            FakeDriverCommandRunner runner = new(0);
            FakeRiskyChangePreflightService preflightService = new(
                new RiskyChangePreflightResult(
                    false,
                    false,
                    false,
                    false,
                    DateTimeOffset.Now,
                    "AegisTune blocked the driver install.",
                    "Create a restore point first."));
            FakeUndoJournalStore undoJournalStore = new();
            PnpUtilDriverInstallService service = new(runner, preflightService, undoJournalStore);
            DriverRepositoryCandidate candidate = new(
                infPath,
                tempDirectory,
                "Contoso",
                "Net",
                "1.0.0.0",
                "device.cat",
                DriverRepositoryMatchKind.ExactHardwareId,
                ["PCI\\VEN_1234&DEV_5678"]);

            DriverInstallExecutionResult result = await service.InstallAsync(
                CreateDevice(),
                candidate,
                dryRunEnabled: false);

            Assert.False(result.Succeeded);
            Assert.Equal("AegisTune blocked the driver install.", result.StatusLine);
            Assert.Null(runner.LastArguments);
            Assert.Equal(1, preflightService.CallCount);
            Assert.Empty(undoJournalStore.Entries);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static DriverDeviceRecord CreateDevice() =>
        new(
            "Contoso Wi-Fi",
            "Net",
            "Contoso",
            "Microsoft",
            "1.0.0.0",
            "Error",
            28,
            "PCI\\VEN_1234&DEV_5678",
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

        public Task<int> RunElevatedAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken = default)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            return Task.FromResult(_exitCode);
        }
    }

    private sealed class FakeRiskyChangePreflightService : IRiskyChangePreflightService
    {
        private readonly RiskyChangePreflightResult _result;

        public FakeRiskyChangePreflightService(RiskyChangePreflightResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<RiskyChangePreflightResult> PrepareAsync(
            RiskyChangePreflightRequest request,
            bool dryRunEnabled,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeUndoJournalStore : IUndoJournalStore
    {
        public string StoragePath => Path.Combine(Path.GetTempPath(), "undo-journal.json");

        public List<UndoJournalEntry> Entries { get; } = [];

        public Task<IReadOnlyList<UndoJournalEntry>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UndoJournalEntry>>(Entries);

        public Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
