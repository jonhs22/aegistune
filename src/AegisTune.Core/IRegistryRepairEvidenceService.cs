namespace AegisTune.Core;

public interface IRegistryRepairEvidenceService
{
    Task<IReadOnlyList<RepairCandidateRecord>> GetCandidatesAsync(CancellationToken cancellationToken = default);
}
