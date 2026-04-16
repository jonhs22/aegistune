namespace AegisTune.Core;

public interface IDriverDepotService
{
    Task<DriverDepotScanResult> ScanAsync(
        IReadOnlyList<string> repositoryRoots,
        IReadOnlyList<DriverDeviceRecord> devices,
        CancellationToken cancellationToken = default);
}
