namespace AegisTune.Core;

public interface IRiskyChangePreflightService
{
    Task<RiskyChangePreflightResult> PrepareAsync(
        RiskyChangePreflightRequest request,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
