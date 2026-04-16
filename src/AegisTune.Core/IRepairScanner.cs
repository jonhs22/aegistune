namespace AegisTune.Core;

public interface IRepairScanner
{
    Task<RepairScanResult> ScanAsync(
        AppInventorySnapshot? appInventory = null,
        StartupInventorySnapshot? startupInventory = null,
        CancellationToken cancellationToken = default);
}
