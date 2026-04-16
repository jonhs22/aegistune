namespace AegisTune.Core;

public interface IAppUpdateService
{
    AppUpdateState CurrentState { get; }

    Task<AppUpdateState> RefreshAsync(bool respectLaunchPreference, CancellationToken cancellationToken = default);

    Task<AppReleaseNotesState> GetReleaseNotesAsync(CancellationToken cancellationToken = default);
}
