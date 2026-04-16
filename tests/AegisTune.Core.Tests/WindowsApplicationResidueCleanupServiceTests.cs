using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class WindowsApplicationResidueCleanupServiceTests
{
    [Fact]
    public async Task CleanupAsync_InDryRun_DoesNotMoveResidueOrAppendUndoEntry()
    {
        string rootPath = CreateTempDirectory();
        string residuePath = Path.Combine(rootPath, "Contoso Cleanup");
        Directory.CreateDirectory(residuePath);
        await File.WriteAllTextAsync(Path.Combine(residuePath, "leftover.log"), "leftover");

        FakeSettingsStore settingsStore = new(new AppSettings());
        FakeUndoJournalStore undoJournalStore = new();
        WindowsApplicationResidueCleanupService service = new(settingsStore, undoJournalStore);

        try
        {
            ApplicationResidueCleanupExecutionResult result = await service.CleanupAsync(
                CreateApplication(residuePath),
                dryRunEnabled: true);

            Assert.True(result.Succeeded);
            Assert.True(result.WasDryRun);
            Assert.True(Directory.Exists(residuePath));
            Assert.Empty(undoJournalStore.Entries);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupAsync_InLiveMode_MovesResidueAndAppendsUndoEntry()
    {
        string rootPath = CreateTempDirectory();
        string residuePath = Path.Combine(rootPath, "Contoso Cleanup");
        Directory.CreateDirectory(residuePath);
        await File.WriteAllTextAsync(Path.Combine(residuePath, "leftover.log"), "leftover");

        FakeSettingsStore settingsStore = new(new AppSettings());
        FakeUndoJournalStore undoJournalStore = new();
        WindowsApplicationResidueCleanupService service = new(settingsStore, undoJournalStore);

        try
        {
            ApplicationResidueCleanupExecutionResult result = await service.CleanupAsync(
                CreateApplication(residuePath),
                dryRunEnabled: false);

            Assert.True(result.Succeeded);
            Assert.False(result.WasDryRun);
            Assert.False(Directory.Exists(residuePath));
            Assert.True(Directory.Exists(result.QuarantinePath));
            Assert.True(File.Exists(Path.Combine(result.QuarantinePath!, "cleanup-manifest.txt")));
            Assert.Single(undoJournalStore.Entries);
            Assert.Equal(UndoJournalEntryKind.ApplicationResidueCleanup, undoJournalStore.Entries[0].Kind);
            Assert.Contains("Residue quarantine:", undoJournalStore.Entries[0].ArtifactLabel, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
            if (undoJournalStore.Entries.Count > 0 && Directory.Exists(undoJournalStore.Entries[0].ArtifactPath))
            {
                Directory.Delete(undoJournalStore.Entries[0].ArtifactPath!, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CleanupAsync_WhenResiduePathIsExcluded_SkipsCleanup()
    {
        string rootPath = CreateTempDirectory();
        string residuePath = Path.Combine(rootPath, "Contoso Cleanup");
        Directory.CreateDirectory(residuePath);
        await File.WriteAllTextAsync(Path.Combine(residuePath, "leftover.log"), "leftover");

        FakeSettingsStore settingsStore = new(new AppSettings(CleanupExclusionPatterns: residuePath));
        FakeUndoJournalStore undoJournalStore = new();
        WindowsApplicationResidueCleanupService service = new(settingsStore, undoJournalStore);

        try
        {
            ApplicationResidueCleanupExecutionResult result = await service.CleanupAsync(
                CreateApplication(residuePath),
                dryRunEnabled: false);

            Assert.True(result.Succeeded);
            Assert.Equal(0, result.MovedFolderCount);
            Assert.True(Directory.Exists(residuePath));
            Assert.Empty(undoJournalStore.Entries);
            Assert.Contains("excluded", result.StatusLine, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static InstalledApplicationRecord CreateApplication(string residuePath) =>
        new(
            "Contoso Cleanup",
            "1.0.0",
            "Contoso",
            InstalledApplicationSource.DesktopRegistry,
            "Current user (default)",
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup",
            InstallLocation: null,
            InstallLocationExists: false,
            UninstallCommand: null,
            ResolvedUninstallTargetPath: null,
            UninstallTargetExists: false,
            EstimatedSizeBytes: null,
            ResidueEvidence:
            [
                new ApplicationResidueRecord(residuePath, "Local AppData", 8, 1)
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
