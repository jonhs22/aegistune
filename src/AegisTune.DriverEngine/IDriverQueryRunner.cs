namespace AegisTune.DriverEngine;

public interface IDriverQueryRunner
{
    Task<DriverQueryExecutionResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default);
}
