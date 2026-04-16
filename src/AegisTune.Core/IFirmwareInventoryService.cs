namespace AegisTune.Core;

public interface IFirmwareInventoryService
{
    Task<FirmwareInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
