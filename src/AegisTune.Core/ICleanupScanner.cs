namespace AegisTune.Core;

public interface ICleanupScanner
{
    Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default);
}
