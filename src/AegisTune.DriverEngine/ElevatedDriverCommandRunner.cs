using System.Diagnostics;

namespace AegisTune.DriverEngine;

public sealed class ElevatedDriverCommandRunner : IDriverCommandRunner
{
    public async Task<int> RunElevatedAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
