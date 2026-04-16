namespace AegisTune.Core;

public interface IUndoJournalStore
{
    string StoragePath { get; }

    Task<IReadOnlyList<UndoJournalEntry>> LoadAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default);
}
