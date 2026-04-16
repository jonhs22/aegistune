namespace AegisTune.RepairEngine;

public interface IRepairAdvisoryExportService
{
    Task<RepairAdvisoryExportResult> ExportAsync(
        RepairAdvisoryExportRequest advisory,
        CancellationToken cancellationToken = default);
}
