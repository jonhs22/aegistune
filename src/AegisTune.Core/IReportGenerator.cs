namespace AegisTune.Core;

public interface IReportGenerator
{
    Task<MaintenanceReportRecord> GenerateAsync(CancellationToken cancellationToken = default);
}
