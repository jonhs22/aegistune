using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.CleanupEngine;

[SupportedOSPlatform("windows")]
public sealed class CleanupScanner : ICleanupScanner
{
    private readonly IRecycleBinShell _recycleBinShell;

    public CleanupScanner()
        : this(new WindowsRecycleBinShell())
    {
    }

    public CleanupScanner(IRecycleBinShell recycleBinShell)
    {
        _recycleBinShell = recycleBinShell ?? throw new ArgumentNullException(nameof(recycleBinShell));
    }

    public Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            DateTimeOffset scannedAt = DateTimeOffset.Now;

            try
            {
                var targets = new[]
                {
                    BuildDirectoryTarget(
                        "User temp",
                        "Clears per-user temporary folders and stale application caches.",
                        Path.GetTempPath(),
                        enabledByDefault: true,
                        supportsExecution: true,
                        cancellationToken),
                    BuildDirectoryTarget(
                        "System temp",
                        "Targets non-critical temporary files created by installers and system tasks.",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
                        enabledByDefault: true,
                        supportsExecution: true,
                        cancellationToken),
                    BuildRecycleBinTarget(_recycleBinShell, cancellationToken),
                    new CleanupTargetScanResult(
                        "Scoped browser traces",
                        "Keeps browser cleanup opt-in and tied to an explicit setting.",
                        "Browser-specific handlers are not enabled yet.",
                        CleanupTargetStatus.Skipped,
                        EnabledByDefault: false,
                        FileCount: 0,
                        ReclaimableBytes: 0,
                        Notes: "This target stays deferred until dedicated browser inventory handlers are implemented.",
                        SupportsExecution: false)
                };

                return new CleanupScanResult(targets, scannedAt);
            }
            catch (Exception ex)
            {
                return new CleanupScanResult(Array.Empty<CleanupTargetScanResult>(), scannedAt, $"Cleanup scan failed: {ex.Message}");
            }
        }, cancellationToken);

    private static CleanupTargetScanResult BuildDirectoryTarget(
        string title,
        string description,
        string path,
        bool enabledByDefault,
        bool supportsExecution,
        CancellationToken cancellationToken)
    {
        DirectoryScanMetrics metrics = ScanDirectory(path, cancellationToken);

        return new CleanupTargetScanResult(
            title,
            description,
            path,
            metrics.Status,
            enabledByDefault,
            metrics.FileCount,
            metrics.TotalBytes,
            metrics.Note,
            supportsExecution);
    }

    private static CleanupTargetScanResult BuildRecycleBinTarget(
        IRecycleBinShell recycleBinShell,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecycleBinSnapshot snapshot = recycleBinShell.Query();
        CleanupTargetStatus status = !snapshot.IsAvailable
            ? CleanupTargetStatus.Error
            : snapshot.HasFindings
                ? CleanupTargetStatus.Ready
                : CleanupTargetStatus.Empty;

        return new CleanupTargetScanResult(
            "Recycle Bin",
            "Measures reclaimable size and supports guided empty for the Windows Recycle Bin.",
            "Fixed drives",
            status,
            EnabledByDefault: true,
            ClampItemCount(snapshot.ItemCount),
            Math.Max(0, snapshot.TotalBytes),
            snapshot.Note ?? (status == CleanupTargetStatus.Error ? "Recycle Bin telemetry could not be accessed." : null),
            SupportsExecution: snapshot.IsAvailable);
    }

    private static int ClampItemCount(long itemCount)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        return itemCount >= int.MaxValue
            ? int.MaxValue
            : (int)itemCount;
    }

    private static DirectoryScanMetrics ScanDirectory(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new DirectoryScanMetrics(CleanupTargetStatus.Error, 0, 0, "Path is unavailable.");
        }

        if (!Directory.Exists(path))
        {
            return new DirectoryScanMetrics(CleanupTargetStatus.Empty, 0, 0, "Folder is not present on this system.");
        }

        long totalBytes = 0;
        int totalFiles = 0;
        int skippedPaths = 0;
        var directories = new Stack<string>();
        directories.Push(path);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDirectory = directories.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                if (string.Equals(currentDirectory, path, StringComparison.OrdinalIgnoreCase))
                {
                    return new DirectoryScanMetrics(CleanupTargetStatus.Error, 0, 0, "Access to this target is denied for the current session.");
                }

                skippedPaths++;
                continue;
            }
            catch (IOException)
            {
                skippedPaths++;
                continue;
            }

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    FileInfo info = new(file);
                    totalBytes += info.Length;
                    totalFiles++;
                }
                catch (UnauthorizedAccessException)
                {
                    skippedPaths++;
                }
                catch (IOException)
                {
                    skippedPaths++;
                }
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                skippedPaths++;
                continue;
            }
            catch (IOException)
            {
                skippedPaths++;
                continue;
            }

            foreach (string childDirectory in childDirectories)
            {
                directories.Push(childDirectory);
            }
        }

        string? note = skippedPaths == 0
            ? null
            : $"Skipped {skippedPaths:N0} protected path(s) while scanning.";

        CleanupTargetStatus status = totalFiles > 0 || totalBytes > 0
            ? CleanupTargetStatus.Ready
            : CleanupTargetStatus.Empty;

        return new DirectoryScanMetrics(status, totalFiles, totalBytes, note);
    }

    private sealed record DirectoryScanMetrics(
        CleanupTargetStatus Status,
        int FileCount,
        long TotalBytes,
        string? Note);
}
