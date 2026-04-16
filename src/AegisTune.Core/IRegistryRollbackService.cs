namespace AegisTune.Core;

public interface IRegistryRollbackService
{
    Task<RegistryRollbackExecutionResult> RollbackAsync(
        UndoJournalEntry entry,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
