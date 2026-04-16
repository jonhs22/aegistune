namespace AegisTune.Core;

public interface IDriverRepositorySeedService
{
    Task<DriverRepositorySeedResult> ExportInstalledPackageAsync(
        DriverDeviceRecord device,
        string targetRoot,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
