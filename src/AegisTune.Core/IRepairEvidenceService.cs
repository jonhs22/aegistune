namespace AegisTune.Core;

public interface IRepairEvidenceService
{
    Task<IReadOnlyList<DependencyRepairSignal>> GetDependencySignalsAsync(
        CancellationToken cancellationToken = default);
}
