namespace AegisTune.Core;

public interface IFirmwareReleaseLookupService
{
    Task<FirmwareReleaseLookupResult> LookupAsync(
        FirmwareInventorySnapshot firmware,
        CancellationToken cancellationToken = default);
}
