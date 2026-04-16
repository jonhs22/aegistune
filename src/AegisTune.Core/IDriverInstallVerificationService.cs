namespace AegisTune.Core;

public interface IDriverInstallVerificationService
{
    DriverInstallVerificationResult Verify(
        DriverDeviceRecord before,
        DriverDeviceRecord? after,
        DriverRepositoryCandidate candidate);
}
