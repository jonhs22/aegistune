namespace AegisTune.Core;

public sealed record DriverRepositorySeedResult(
    string SourceInfName,
    string TargetRoot,
    string ExportDirectory,
    string CommandLine,
    bool WasDryRun,
    bool Succeeded,
    int? ExitCode,
    DateTimeOffset ExecutedAt,
    string StatusLine,
    string GuidanceLine)
{
    public string ExecutedAtLabel => ExecutedAt.ToLocalTime().ToString("g");

    public string ExitCodeLabel => ExitCode?.ToString() ?? "Not executed";
}
