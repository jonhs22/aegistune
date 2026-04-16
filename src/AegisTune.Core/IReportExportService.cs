namespace AegisTune.Core;

public interface IReportExportService
{
    Task<ReportExportResult> ExportAsync(
        MaintenanceReportRecord report,
        CancellationToken cancellationToken = default);
}
