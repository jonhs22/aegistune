namespace AegisTune.Core;

public sealed record DriverInstallExecutionResult(
    string InfPath,
    string CommandLine,
    bool WasDryRun,
    bool Succeeded,
    int? ExitCode,
    DateTimeOffset ExecutedAt,
    string StatusLine,
    string VerificationHint)
{
    public string ExecutedAtLabel => ExecutedAt.ToLocalTime().ToString("g");

    public string ExitCodeLabel => ExitCode?.ToString() ?? "Not executed";
}
