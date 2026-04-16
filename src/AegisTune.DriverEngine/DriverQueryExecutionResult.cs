namespace AegisTune.DriverEngine;

public sealed record DriverQueryExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
