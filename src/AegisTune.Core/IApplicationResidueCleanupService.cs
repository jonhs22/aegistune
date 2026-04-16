namespace AegisTune.Core;

public interface IApplicationResidueCleanupService
{
    Task<ApplicationResidueCleanupExecutionResult> CleanupAsync(
        InstalledApplicationRecord application,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
