namespace AegisTune.Core;

public interface ICleanupExecutionService
{
    Task<CleanupExecutionResult> ExecuteAsync(
        IReadOnlyList<CleanupTargetScanResult> selectedTargets,
        AppSettings settings,
        CancellationToken cancellationToken = default);
}
