namespace AegisTune.Core;

public interface IWindowsHealthService
{
    Task<WindowsHealthSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
