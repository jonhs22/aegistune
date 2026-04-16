namespace AegisTune.Core;

public interface ISystemRestoreService
{
    Task<SystemRestoreCheckpointResult> CreateCheckpointAsync(
        string description,
        SystemRestoreIntent intent,
        CancellationToken cancellationToken = default);
}
