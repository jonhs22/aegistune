using AegisTune.Core;
using AegisTune.SystemIntegration;
using Microsoft.Win32;

namespace AegisTune.Core.Tests;

public sealed class WindowsStartupEntryActionServiceTests
{
    private const string BackupRootPath = @"Software\AegisTune\DisabledStartupEntries";

    [Fact]
    public async Task DisableEntryAsync_RegistryEntry_AppendsUndoJournalEntry()
    {
        string subKeyPath = $@"Software\AegisTune.Tests\Startup\{Guid.NewGuid():N}";
        string registryLocation = $@"HKEY_CURRENT_USER\{subKeyPath}";
        HashSet<string> backupKeysBefore = GetBackupSubKeyNames();

        using RegistryKey testKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
        testKey.SetValue("ContosoAgent", "\"C:\\Tools\\contoso.exe\" --startup");

        FakeUndoJournalStore undoJournalStore = new();
        WindowsStartupEntryActionService service = new(
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

        try
        {
            StartupEntryActionResult result = await service.DisableEntryAsync(
                new StartupEntryRecord(
                    "Contoso Agent",
                    "\"C:\\Tools\\contoso.exe\" --startup",
                    "Registry",
                    "Current user",
                    @"C:\Tools\contoso.exe",
                    true,
                    false,
                    StartupImpactLevel.Medium,
                    StartupEntryOrigin.RegistryValue,
                    registryLocation,
                    "ContosoAgent",
                    nameof(RegistryView.Default)));

            Assert.True(result.Succeeded);
            using RegistryKey? keyAfter = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: false);
            Assert.NotNull(keyAfter);
            Assert.DoesNotContain("ContosoAgent", keyAfter!.GetValueNames());

            Assert.Single(undoJournalStore.Entries);
            Assert.Equal(UndoJournalEntryKind.StartupDisable, undoJournalStore.Entries[0].Kind);
            Assert.Equal("Startup source: HKEY_CURRENT_USER\\" + subKeyPath + " [ContosoAgent]", undoJournalStore.Entries[0].TargetDetailLabel);
            Assert.Contains("Launch command:", undoJournalStore.Entries[0].CommandLineSummary, StringComparison.Ordinal);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
            DeleteNewBackupKeys(backupKeysBefore);
        }
    }

    [Fact]
    public async Task RemoveOrphanedEntryAsync_RegistryEntry_AppendsCleanupUndoJournalEntry()
    {
        string subKeyPath = $@"Software\AegisTune.Tests\Startup\{Guid.NewGuid():N}";
        string registryLocation = $@"HKEY_CURRENT_USER\{subKeyPath}";

        using RegistryKey testKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
        testKey.SetValue("OldContosoAgent", "\"C:\\Missing\\contoso.exe\" --startup");

        FakeUndoJournalStore undoJournalStore = new();
        WindowsStartupEntryActionService service = new(
            new FakeRiskyChangePreflightService(
                new RiskyChangePreflightResult(
                    true,
                    false,
                    true,
                    false,
                    DateTimeOffset.Now,
                    "Reused the existing Windows restore point window.",
                    "Safe to continue.")),
            undoJournalStore);

        try
        {
            StartupEntryActionResult result = await service.RemoveOrphanedEntryAsync(
                new StartupEntryRecord(
                    "Old Contoso Agent",
                    "\"C:\\Missing\\contoso.exe\" --startup",
                    "Registry",
                    "Current user",
                    @"C:\Missing\contoso.exe",
                    false,
                    true,
                    StartupImpactLevel.Review,
                    StartupEntryOrigin.RegistryValue,
                    registryLocation,
                    "OldContosoAgent",
                    nameof(RegistryView.Default)));

            Assert.True(result.Succeeded);
            using RegistryKey? keyAfter = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: false);
            Assert.NotNull(keyAfter);
            Assert.DoesNotContain("OldContosoAgent", keyAfter!.GetValueNames());

            Assert.Single(undoJournalStore.Entries);
            Assert.Equal(UndoJournalEntryKind.StartupCleanup, undoJournalStore.Entries[0].Kind);
            Assert.Contains("Startup cleanup", undoJournalStore.Entries[0].Title, StringComparison.Ordinal);
            Assert.Contains("Startup source:", undoJournalStore.Entries[0].TargetDetailLabel, StringComparison.Ordinal);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public async Task RestoreDisabledEntryAsync_RegistryEntry_RestoresSavedValueAndAppendsUndoJournalEntry()
    {
        string subKeyPath = $@"Software\AegisTune.Tests\Startup\{Guid.NewGuid():N}";
        string registryLocation = $@"HKEY_CURRENT_USER\{subKeyPath}";
        string launchCommand = "\"C:\\Tools\\contoso.exe\" --startup";
        HashSet<string> backupKeysBefore = GetBackupSubKeyNames();

        using RegistryKey testKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
        testKey.SetValue("ContosoAgent", launchCommand, RegistryValueKind.ExpandString);

        FakeUndoJournalStore undoJournalStore = new();
        WindowsStartupEntryActionService service = new(
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

        try
        {
            StartupEntryRecord startupEntry = new(
                "Contoso Agent",
                launchCommand,
                "Registry",
                "Current user",
                @"C:\Tools\contoso.exe",
                true,
                false,
                StartupImpactLevel.Medium,
                StartupEntryOrigin.RegistryValue,
                registryLocation,
                "ContosoAgent",
                nameof(RegistryView.Default));

            StartupEntryActionResult disableResult = await service.DisableEntryAsync(startupEntry);
            Assert.True(disableResult.Succeeded);

            UndoJournalEntry disableEntry = Assert.Single(undoJournalStore.Entries);
            StartupEntryActionResult restoreResult = await service.RestoreDisabledEntryAsync(disableEntry, dryRunEnabled: false);

            Assert.True(restoreResult.Succeeded);

            using RegistryKey? keyAfter = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: false);
            Assert.NotNull(keyAfter);
            Assert.Equal(launchCommand, keyAfter!.GetValue("ContosoAgent")?.ToString());
            Assert.Equal(RegistryValueKind.ExpandString, keyAfter.GetValueKind("ContosoAgent"));

            Assert.Equal(2, undoJournalStore.Entries.Count);
            Assert.Equal(UndoJournalEntryKind.StartupRestore, undoJournalStore.Entries[1].Kind);
            Assert.Contains("Startup restore", undoJournalStore.Entries[1].Title, StringComparison.Ordinal);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
            DeleteNewBackupKeys(backupKeysBefore);
        }
    }

    [Fact]
    public async Task RestoreDisabledEntryAsync_StartupFile_RestoresMovedFileAndAppendsUndoJournalEntry()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "AegisTune.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string startupFilePath = Path.Combine(tempDirectory, "ContosoAgent.lnk");
        await File.WriteAllTextAsync(startupFilePath, "startup-link");

        FakeUndoJournalStore undoJournalStore = new();
        WindowsStartupEntryActionService service = new(
            new FakeRiskyChangePreflightService(
                new RiskyChangePreflightResult(
                    true,
                    false,
                    false,
                    false,
                    DateTimeOffset.Now,
                    "Restore point policy is ready.",
                    "Safe to continue.")),
            undoJournalStore);

        try
        {
            StartupEntryRecord startupEntry = new(
                "Contoso Agent",
                "\"C:\\Tools\\contoso.exe\" --startup",
                "Startup folder",
                "Current user",
                @"C:\Tools\contoso.exe",
                true,
                false,
                StartupImpactLevel.Low,
                StartupEntryOrigin.StartupFolderFile,
                StartupFilePath: startupFilePath);

            StartupEntryActionResult disableResult = await service.DisableEntryAsync(startupEntry);
            Assert.True(disableResult.Succeeded);
            Assert.False(File.Exists(startupFilePath));

            UndoJournalEntry disableEntry = Assert.Single(undoJournalStore.Entries);
            StartupEntryActionResult restoreResult = await service.RestoreDisabledEntryAsync(disableEntry, dryRunEnabled: false);

            Assert.True(restoreResult.Succeeded);
            Assert.True(File.Exists(startupFilePath));
            Assert.False(File.Exists(disableEntry.ArtifactPath));
            Assert.Equal(2, undoJournalStore.Entries.Count);
            Assert.Equal(UndoJournalEntryKind.StartupRestore, undoJournalStore.Entries[1].Kind);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static HashSet<string> GetBackupSubKeyNames()
    {
        using RegistryKey? backupRoot = Registry.CurrentUser.OpenSubKey(BackupRootPath, writable: false);
        return backupRoot?.GetSubKeyNames().ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];
    }

    private static void DeleteNewBackupKeys(HashSet<string> backupKeysBefore)
    {
        using RegistryKey? backupRoot = Registry.CurrentUser.OpenSubKey(BackupRootPath, writable: false);
        if (backupRoot is null)
        {
            return;
        }

        string[] newKeyNames = backupRoot
            .GetSubKeyNames()
            .Except(backupKeysBefore, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        backupRoot.Dispose();

        foreach (string keyName in newKeyNames)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{BackupRootPath}\{keyName}", throwOnMissingSubKey: false);
        }
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
