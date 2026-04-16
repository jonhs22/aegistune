using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class WindowsRiskyChangePreflightServiceTests
{
    [Fact]
    public async Task PrepareAsync_WhenRestorePointSettingIsDisabled_AllowsProceedWithoutCallingRestoreService()
    {
        FakeSettingsStore settingsStore = new(new AppSettings(CreateRestorePointBeforeFixes: false));
        FakeSystemRestoreService restoreService = new(
            new SystemRestoreCheckpointResult(
                true,
                "unused",
                DateTimeOffset.Now,
                "Created restore point.",
                "Safe to continue."));
        FakeUndoJournalStore undoJournalStore = new();
        WindowsRiskyChangePreflightService service = new(settingsStore, restoreService, undoJournalStore, () => true);

        RiskyChangePreflightResult result = await service.PrepareAsync(
            CreateRequest(),
            dryRunEnabled: false);

        Assert.True(result.ShouldProceed);
        Assert.Equal(0, restoreService.CallCount);
        Assert.Empty(undoJournalStore.Entries);
        Assert.Contains("disabled in Settings", result.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareAsync_WhenRestorePointCreationFails_BlocksChange()
    {
        FakeSettingsStore settingsStore = new(new AppSettings(CreateRestorePointBeforeFixes: true));
        FakeSystemRestoreService restoreService = new(
            new SystemRestoreCheckpointResult(
                false,
                "driver checkpoint",
                DateTimeOffset.Now,
                "Windows did not create a restore point.",
                "Enable System Protection and retry."));
        FakeUndoJournalStore undoJournalStore = new();
        WindowsRiskyChangePreflightService service = new(settingsStore, restoreService, undoJournalStore, () => true);

        RiskyChangePreflightResult result = await service.PrepareAsync(
            CreateRequest(),
            dryRunEnabled: false);

        Assert.False(result.ShouldProceed);
        Assert.Equal(1, restoreService.CallCount);
        Assert.Empty(undoJournalStore.Entries);
        Assert.Contains("blocked", result.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Enable System Protection", result.GuidanceLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareAsync_ReusesRecentCheckpointWithinSessionWindow()
    {
        FakeSettingsStore settingsStore = new(new AppSettings(CreateRestorePointBeforeFixes: true));
        FakeSystemRestoreService restoreService = new(
            new SystemRestoreCheckpointResult(
                true,
                "driver checkpoint",
                DateTimeOffset.Now,
                "Created restore point.",
                "Safe to continue."));
        FakeUndoJournalStore undoJournalStore = new();
        WindowsRiskyChangePreflightService service = new(settingsStore, restoreService, undoJournalStore, () => true);

        RiskyChangePreflightResult first = await service.PrepareAsync(CreateRequest(), dryRunEnabled: false);
        RiskyChangePreflightResult second = await service.PrepareAsync(CreateRequest(), dryRunEnabled: false);

        Assert.True(first.ShouldProceed);
        Assert.True(first.RestorePointCreated);
        Assert.True(second.ShouldProceed);
        Assert.True(second.RestorePointReused);
        Assert.Equal(1, restoreService.CallCount);
        Assert.Single(undoJournalStore.Entries);
    }

    private static RiskyChangePreflightRequest CreateRequest() =>
        new(
            RiskyChangeType.DriverInstall,
            "Driver install for Contoso Wi-Fi",
            SystemRestoreIntent.DeviceDriverInstall);

    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly AppSettings _settings;

        public FakeSettingsStore(AppSettings settings)
        {
            _settings = settings;
        }

        public string StoragePath => "memory";

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeSystemRestoreService : ISystemRestoreService
    {
        private readonly SystemRestoreCheckpointResult _result;

        public FakeSystemRestoreService(SystemRestoreCheckpointResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<SystemRestoreCheckpointResult> CreateCheckpointAsync(
            string description,
            SystemRestoreIntent intent,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeUndoJournalStore : IUndoJournalStore
    {
        public string StoragePath => "memory";

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
