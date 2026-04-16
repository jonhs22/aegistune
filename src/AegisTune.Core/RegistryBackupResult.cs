namespace AegisTune.Core;

public sealed record RegistryBackupResult(
    bool Succeeded,
    string RegistryPath,
    string? BackupFilePath,
    DateTimeOffset ProcessedAt,
    string StatusLine)
{
    public string ProcessedAtLabel => ProcessedAt.ToLocalTime().ToString("g");
}
