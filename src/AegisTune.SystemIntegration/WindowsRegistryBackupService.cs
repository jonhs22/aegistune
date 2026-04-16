using System.Diagnostics;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

public sealed class WindowsRegistryBackupService : IRegistryBackupService
{
    private readonly string _backupRoot;

    public WindowsRegistryBackupService(string? backupRoot = null)
    {
        _backupRoot = backupRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "RegistryBackups");
    }

    public async Task<RegistryBackupResult> BackupKeyAsync(
        string registryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registryPath);

        DateTimeOffset processedAt = DateTimeOffset.Now;
        string safeName = SanitizeFileName(registryPath);
        string directory = Path.Combine(_backupRoot, processedAt.ToString("yyyyMMdd"));
        string backupFilePath = Path.Combine(directory, $"{processedAt:HHmmssfff}-{safeName}.reg");

        Directory.CreateDirectory(directory);

        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"export \"{registryPath}\" \"{backupFilePath}\" /y",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return new RegistryBackupResult(
                false,
                registryPath,
                null,
                processedAt,
                string.IsNullOrWhiteSpace(stderr)
                    ? $"Registry backup failed for {registryPath}. {stdout.Trim()}"
                    : $"Registry backup failed for {registryPath}. {stderr.Trim()}");
        }

        return new RegistryBackupResult(
            true,
            registryPath,
            backupFilePath,
            processedAt,
            $"Exported a registry backup for {registryPath}.");
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(character =>
                Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character)
            .ToArray());

        return sanitized.Length > 120
            ? sanitized[..120]
            : sanitized;
    }
}
