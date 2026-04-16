using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class WindowsApplicationUninstallServiceTests
{
    [Fact]
    public async Task UninstallAsync_InDryRun_DoesNotAppendUndoJournalEntry()
    {
        FakeUndoJournalStore undoJournalStore = new();
        WindowsApplicationUninstallService service = new(
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

        InstalledApplicationRecord application = CreateApplication("cmd.exe /c exit 0");

        ApplicationUninstallExecutionResult result = await service.UninstallAsync(
            application,
            dryRunEnabled: true);

        Assert.True(result.Succeeded);
        Assert.True(result.WasDryRun);
        Assert.False(result.WorkflowLaunched);
        Assert.Empty(undoJournalStore.Entries);
        Assert.Contains("previewed", result.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UninstallAsync_InLiveMode_AppendsUndoJournalEntry()
    {
        FakeUndoJournalStore undoJournalStore = new();
        WindowsApplicationUninstallService service = new(
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

        InstalledApplicationRecord application = CreateApplication("cmd.exe /c exit 0");

        ApplicationUninstallExecutionResult result = await service.UninstallAsync(
            application,
            dryRunEnabled: false);

        Assert.True(result.Succeeded);
        Assert.True(result.WorkflowLaunched);
        Assert.True(result.CompletedWithinProbeWindow);
        Assert.Equal(0, result.ExitCode);
        Assert.Single(undoJournalStore.Entries);
        Assert.Equal(UndoJournalEntryKind.ApplicationUninstall, undoJournalStore.Entries[0].Kind);
        Assert.Contains("Uninstall command:", undoJournalStore.Entries[0].CommandLineSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UninstallAsync_WhenPreflightBlocks_DoesNotAppendUndoJournalEntry()
    {
        FakeUndoJournalStore undoJournalStore = new();
        WindowsApplicationUninstallService service = new(
            new FakeRiskyChangePreflightService(
                new RiskyChangePreflightResult(
                    false,
                    false,
                    false,
                    false,
                    DateTimeOffset.Now,
                    "AegisTune blocked the uninstall workflow.",
                    "Create a restore point first.")),
            undoJournalStore);

        InstalledApplicationRecord application = CreateApplication("cmd.exe /c exit 0");

        ApplicationUninstallExecutionResult result = await service.UninstallAsync(
            application,
            dryRunEnabled: false);

        Assert.False(result.Succeeded);
        Assert.Contains("blocked", result.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(undoJournalStore.Entries);
    }

    private static InstalledApplicationRecord CreateApplication(string uninstallCommand)
    {
        string commandPath = Environment.GetEnvironmentVariable("ComSpec")
            ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");

        return new InstalledApplicationRecord(
            "Contoso Cleanup",
            "1.0.0",
            "Contoso",
            InstalledApplicationSource.DesktopRegistry,
            "Current user (default)",
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup",
            InstallLocation: null,
            InstallLocationExists: false,
            UninstallCommand: uninstallCommand,
            ResolvedUninstallTargetPath: commandPath,
            UninstallTargetExists: File.Exists(commandPath),
            EstimatedSizeBytes: null);
    }

    private sealed class FakeRiskyChangePreflightService : IRiskyChangePreflightService
    {
        private readonly RiskyChangePreflightResult _result;

        public FakeRiskyChangePreflightService(RiskyChangePreflightResult result)
        {
            _result = result;
        }

        public Task<RiskyChangePreflightResult> PrepareAsync(
            RiskyChangePreflightRequest request,
            bool dryRunEnabled,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
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
