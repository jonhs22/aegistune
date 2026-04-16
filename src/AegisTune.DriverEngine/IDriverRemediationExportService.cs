using AegisTune.Core;

namespace AegisTune.DriverEngine;

public interface IDriverRemediationExportService
{
    Task<DriverRemediationExportResult> ExportAsync(
        DriverDeviceRecord device,
        DriverRemediationPlan plan,
        CancellationToken cancellationToken = default);
}
