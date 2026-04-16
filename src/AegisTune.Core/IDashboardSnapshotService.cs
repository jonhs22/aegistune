namespace AegisTune.Core;

public interface IDashboardSnapshotService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
