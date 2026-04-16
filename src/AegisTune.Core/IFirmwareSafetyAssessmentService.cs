namespace AegisTune.Core;

public interface IFirmwareSafetyAssessmentService
{
    Task<FirmwareSafetyAssessment> AssessAsync(
        FirmwareInventorySnapshot firmware,
        FirmwareReleaseLookupResult? lookupResult = null,
        CancellationToken cancellationToken = default);
}
