namespace AegisTune.Core;

public interface IDriverAuditExportService
{
    Task<DriverAuditExportResult> ExportAsync(
        DeviceInventorySnapshot snapshot,
        CancellationToken cancellationToken = default);
}
