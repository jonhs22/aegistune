namespace AegisTune.Core;

public interface IInstalledApplicationInventoryService
{
    Task<AppInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
