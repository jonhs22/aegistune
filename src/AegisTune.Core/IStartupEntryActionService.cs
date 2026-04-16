namespace AegisTune.Core;

public interface IStartupEntryActionService
{
    Task<StartupEntryActionResult> DisableEntryAsync(
        StartupEntryRecord entry,
        CancellationToken cancellationToken = default);

    Task<StartupEntryActionResult> RemoveOrphanedEntryAsync(
        StartupEntryRecord entry,
        CancellationToken cancellationToken = default);

    Task<StartupEntryActionResult> RestoreDisabledEntryAsync(
        UndoJournalEntry entry,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
