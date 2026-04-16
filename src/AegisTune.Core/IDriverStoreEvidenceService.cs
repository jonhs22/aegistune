namespace AegisTune.Core;

public interface IDriverStoreEvidenceService
{
    Task<DriverStoreDeviceEvidenceResult> CollectAsync(
        DriverDeviceRecord device,
        CancellationToken cancellationToken = default);
}
