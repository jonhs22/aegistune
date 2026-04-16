namespace AegisTune.Core;

public interface IStartupInventoryService
{
    Task<StartupInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
