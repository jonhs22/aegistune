namespace AegisTune.Core;

public interface IDriverInstallService
{
    Task<DriverInstallExecutionResult> InstallAsync(
        DriverDeviceRecord device,
        DriverRepositoryCandidate candidate,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
