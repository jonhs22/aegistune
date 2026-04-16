using System.ComponentModel;

namespace AegisTune.DriverEngine;

public interface IDriverCommandRunner
{
    Task<int> RunElevatedAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default);
}
