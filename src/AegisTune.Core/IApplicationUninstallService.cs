namespace AegisTune.Core;

public interface IApplicationUninstallService
{
    Task<ApplicationUninstallExecutionResult> UninstallAsync(
        InstalledApplicationRecord application,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
