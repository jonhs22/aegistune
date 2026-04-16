namespace AegisTune.Core;

public interface IReportStore
{
    string StoragePath { get; }

    Task<IReadOnlyList<MaintenanceReportRecord>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MaintenanceReportRecord report, CancellationToken cancellationToken = default);
}
