using AegisTune.Core;
using AegisTune.Storage;

namespace AegisTune.Core.Tests;

public sealed class JsonUndoJournalStoreTests
{
    [Fact]
    public async Task AppendAsync_PersistsEntriesInNewestFirstOrder()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "AegisTune.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string storagePath = Path.Combine(tempDirectory, "undo-journal.json");

        try
        {
            JsonUndoJournalStore store = new(storagePath);
            UndoJournalEntry first = new(
                Guid.NewGuid(),
                UndoJournalEntryKind.RestorePoint,
                "First",
                DateTimeOffset.Now.AddMinutes(-5),
                "Created restore point.",
                "Safe to continue.");
            UndoJournalEntry second = new(
                Guid.NewGuid(),
                UndoJournalEntryKind.RegistryRepair,
                "Second",
                DateTimeOffset.Now,
                "Applied repair.",
                "Use backup if needed.",
                RegistryBackupPath: @"C:\Temp\backup.reg");

            await store.AppendAsync(first);
            await store.AppendAsync(second);

            IReadOnlyList<UndoJournalEntry> entries = await store.LoadAsync();
            Assert.Equal(2, entries.Count);
            Assert.Equal("Second", entries[0].Title);
            Assert.Equal("First", entries[1].Title);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
