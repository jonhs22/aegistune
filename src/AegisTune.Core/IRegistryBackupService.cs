namespace AegisTune.Core;

public interface IRegistryBackupService
{
    Task<RegistryBackupResult> BackupKeyAsync(
        string registryPath,
        CancellationToken cancellationToken = default);
}
