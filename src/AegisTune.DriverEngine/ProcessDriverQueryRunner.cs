using System.Diagnostics;

namespace AegisTune.DriverEngine;

public sealed class ProcessDriverQueryRunner : IDriverQueryRunner
{
    public async Task<DriverQueryExecutionResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new DriverQueryExecutionResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }
}
