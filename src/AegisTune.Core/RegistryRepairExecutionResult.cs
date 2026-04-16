namespace AegisTune.Core;

public sealed record RegistryRepairExecutionResult(
    bool Succeeded,
    bool WasDryRun,
    string StatusLine,
    string GuidanceLine,
    DateTimeOffset ProcessedAt,
    string? BackupFilePath = null)
{
    public bool HasBackupFile => !string.IsNullOrWhiteSpace(BackupFilePath);

    public string BackupFileLabel => string.IsNullOrWhiteSpace(BackupFilePath)
        ? "No registry backup file was created."
        : BackupFilePath!;

    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
