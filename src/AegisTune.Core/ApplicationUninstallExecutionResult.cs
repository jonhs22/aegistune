namespace AegisTune.Core;

public sealed record ApplicationUninstallExecutionResult(
    string DisplayName,
    string CommandLine,
    bool WasDryRun,
    bool Succeeded,
    bool WorkflowLaunched,
    bool CompletedWithinProbeWindow,
    int? ExitCode,
    DateTimeOffset ProcessedAt,
    string StatusLine,
    string GuidanceLine)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");

    public string ExitCodeLabel => ExitCode?.ToString() ?? "Not available";
}
