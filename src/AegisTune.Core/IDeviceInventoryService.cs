namespace AegisTune.Core;

public interface IDeviceInventoryService
{
    Task<DeviceInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
