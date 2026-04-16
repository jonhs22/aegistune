using AegisTune.Core;
using AegisTune.SystemIntegration;
using Microsoft.Win32;

namespace AegisTune.Core.Tests;

public sealed class WindowsRegistryRepairExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RemoveRegistryKey_RemovesTargetKey()
    {
        string registryPath = CreateTestRegistryKey("RegistryRepairRemove");
        using (RegistryKey currentUser = Registry.CurrentUser)
        using (RegistryKey testKey = currentUser.CreateSubKey(GetSubKeyPath(registryPath)))
        {
            testKey.SetValue("Sample", "value");
        }

        WindowsRegistryRepairExecutionService service = new(
            new FakeRegistryBackupService(),
            new FakeRiskyChangePreflightService(),
            new FakeUndoJournalStore());

        RegistryRepairExecutionResult result = await service.ExecuteAsync(
            new RepairCandidateRecord(
                "Remove stale key",
                "Registry & leftovers",
                RiskLevel.Review,
                false,
                "Evidence",
                "Remove key",
                registryPath,
                RegistryRepairPackKind: RegistryRepairPackKind.RemoveRegistryKey,
                RegistryPath: registryPath,
                RepairActionLabel: "Back up + remove registry entry"),
            dryRunEnabled: false);

        Assert.True(result.Succeeded);
        Assert.True(result.HasBackupFile);
        using RegistryKey? removedKey = Registry.CurrentUser.OpenSubKey(GetSubKeyPath(registryPath));
        Assert.Null(removedKey);
    }

    [Fact]
    public async Task ExecuteAsync_SetDwordValue_UpdatesServiceStartValue()
    {
        string registryPath = CreateTestRegistryKey("RegistryRepairDword");
        using (RegistryKey currentUser = Registry.CurrentUser)
        using (RegistryKey testKey = currentUser.CreateSubKey(GetSubKeyPath(registryPath)))
        {
            testKey.SetValue("Start", 2, RegistryValueKind.DWord);
        }

        WindowsRegistryRepairExecutionService service = new(
            new FakeRegistryBackupService(),
            new FakeRiskyChangePreflightService(),
            new FakeUndoJournalStore());

        RegistryRepairExecutionResult result = await service.ExecuteAsync(
            new RepairCandidateRecord(
                "Disable broken service",
                "Services & registry",
                RiskLevel.Review,
                false,
                "Evidence",
                "Disable service",
                registryPath,
                RegistryRepairPackKind: RegistryRepairPackKind.SetDwordValue,
                RegistryPath: registryPath,
                RegistryValueName: "Start",
                RegistryDwordValue: 4,
                RepairActionLabel: "Back up + disable service"),
            dryRunEnabled: false);

        Assert.True(result.Succeeded);
        using RegistryKey? updatedKey = Registry.CurrentUser.OpenSubKey(GetSubKeyPath(registryPath));
        Assert.NotNull(updatedKey);
        Assert.Equal(4, Convert.ToInt32(updatedKey!.GetValue("Start")));

        Registry.CurrentUser.DeleteSubKeyTree(GetSubKeyPath(registryPath), throwOnMissingSubKey: false);
    }

    private static string CreateTestRegistryKey(string prefix) =>
        $@"HKEY_CURRENT_USER\Software\AegisTune.Tests\{prefix}_{Guid.NewGuid():N}";

    private static string GetSubKeyPath(string fullRegistryPath) =>
        fullRegistryPath["HKEY_CURRENT_USER\\".Length..];

    private sealed class FakeRegistryBackupService : IRegistryBackupService
    {
        public Task<RegistryBackupResult> BackupKeyAsync(
            string registryPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegistryBackupResult(
                true,
                registryPath,
                Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.reg"),
                DateTimeOffset.Now,
                "Backed up registry key."));
    }

    private sealed class FakeRiskyChangePreflightService : IRiskyChangePreflightService
    {
        public Task<RiskyChangePreflightResult> PrepareAsync(
            RiskyChangePreflightRequest request,
            bool dryRunEnabled,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RiskyChangePreflightResult(
                true,
                true,
                false,
                dryRunEnabled,
                DateTimeOffset.Now,
                "Created a Windows restore point.",
                "Safe to continue."));
    }

    private sealed class FakeUndoJournalStore : IUndoJournalStore
    {
        public string StoragePath => Path.Combine(Path.GetTempPath(), "undo-journal.json");

        public Task<IReadOnlyList<UndoJournalEntry>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UndoJournalEntry>>([]);

        public Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
