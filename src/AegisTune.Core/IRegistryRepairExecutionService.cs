namespace AegisTune.Core;

public interface IRegistryRepairExecutionService
{
    Task<RegistryRepairExecutionResult> ExecuteAsync(
        RepairCandidateRecord candidate,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
