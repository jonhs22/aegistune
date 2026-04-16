using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.CleanupEngine;

[SupportedOSPlatform("windows")]
public sealed class CleanupExecutionService : ICleanupExecutionService
{
    private readonly string _userTempPath;
    private readonly string _systemTempPath;
    private readonly IRecycleBinShell _recycleBinShell;

    public CleanupExecutionService(string? userTempPath = null, string? systemTempPath = null)
        : this(new WindowsRecycleBinShell(), userTempPath, systemTempPath)
    {
    }

    public CleanupExecutionService(
        IRecycleBinShell recycleBinShell,
        string? userTempPath = null,
        string? systemTempPath = null)
    {
        _recycleBinShell = recycleBinShell ?? throw new ArgumentNullException(nameof(recycleBinShell));
        _userTempPath = userTempPath ?? Path.GetTempPath();
        _systemTempPath = systemTempPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
    }

    public Task<CleanupExecutionResult> ExecuteAsync(
        IReadOnlyList<CleanupTargetScanResult> selectedTargets,
        AppSettings settings,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentNullException.ThrowIfNull(selectedTargets);
            ArgumentNullException.ThrowIfNull(settings);

            DateTimeOffset processedAt = DateTimeOffset.Now;
            List<CleanupTargetExecutionResult> results = [];

            foreach (CleanupTargetScanResult target in selectedTargets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!target.SupportsExecution)
                {
                    results.Add(new CleanupTargetExecutionResult(
                        target.Title,
                        Succeeded: true,
                        Skipped: true,
                        DeletedFileCount: 0,
                        ReclaimedBytes: 0,
                        "This cleanup target is preview-only in the current milestone."));
                    continue;
                }

                if (!target.HasFindings)
                {
                    results.Add(new CleanupTargetExecutionResult(
                        target.Title,
                        Succeeded: true,
                        Skipped: true,
                        DeletedFileCount: 0,
                        ReclaimedBytes: 0,
                        "This cleanup target no longer exposes actionable files."));
                    continue;
                }

                if (settings.DryRunEnabled)
                {
                    results.Add(new CleanupTargetExecutionResult(
                        target.Title,
                        Succeeded: true,
                        Skipped: true,
                        DeletedFileCount: 0,
                        ReclaimedBytes: 0,
                        $"Dry-run mode is enabled. AegisTune would clean {target.FormattedReclaimableSize} from {target.Title}."));
                    continue;
                }

                try
                {
                    results.Add(target.Title switch
                    {
                        "User temp" => CleanDirectoryTarget(target.Title, _userTempPath, settings),
                        "System temp" => CleanDirectoryTarget(target.Title, _systemTempPath, settings),
                        "Recycle Bin" => EmptyRecycleBinTarget(target),
                        _ => new CleanupTargetExecutionResult(
                            target.Title,
                            Succeeded: true,
                            Skipped: true,
                            DeletedFileCount: 0,
                            ReclaimedBytes: 0,
                            "This cleanup target does not have an automated handler yet.")
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new CleanupTargetExecutionResult(
                        target.Title,
                        Succeeded: false,
                        Skipped: false,
                        DeletedFileCount: 0,
                        ReclaimedBytes: 0,
                        $"Cleanup failed: {ex.Message}"));
                }
            }

            return new CleanupExecutionResult(processedAt, results);
        }, cancellationToken);

    private CleanupTargetExecutionResult EmptyRecycleBinTarget(CleanupTargetScanResult target)
    {
        RecycleBinSnapshot beforeSnapshot = _recycleBinShell.Query();
        long baselineItems = beforeSnapshot.IsAvailable
            ? beforeSnapshot.ItemCount
            : target.FileCount;
        long baselineBytes = beforeSnapshot.IsAvailable
            ? beforeSnapshot.TotalBytes
            : target.ReclaimableBytes;

        if (baselineItems <= 0 && baselineBytes <= 0)
        {
            return new CleanupTargetExecutionResult(
                target.Title,
                Succeeded: true,
                Skipped: true,
                DeletedFileCount: 0,
                ReclaimedBytes: 0,
                "Recycle Bin was already empty by execution time.");
        }

        _recycleBinShell.Empty();
        RecycleBinSnapshot afterSnapshot = _recycleBinShell.Query();

        long deletedItems = afterSnapshot.IsAvailable
            ? Math.Max(0, baselineItems - afterSnapshot.ItemCount)
            : baselineItems;
        long reclaimedBytes = afterSnapshot.IsAvailable
            ? Math.Max(0, baselineBytes - afterSnapshot.TotalBytes)
            : baselineBytes;

        string message = deletedItems > 0 || reclaimedBytes > 0
            ? $"Emptied the Recycle Bin and reclaimed {DataSizeFormatter.FormatBytes(reclaimedBytes)} across {FormatDeletedFileCount(deletedItems)}."
            : "Recycle Bin empty request completed, but exact reclaim metrics were not available after execution.";

        if (!beforeSnapshot.IsAvailable)
        {
            message += " Baseline metrics were approximated from the last cleanup scan.";
        }

        if (!afterSnapshot.IsAvailable)
        {
            message += " Post-cleanup verification could not refresh the live Recycle Bin counters.";
        }
        else if (afterSnapshot.HasFindings)
        {
            message += $" Verification still sees {DataSizeFormatter.FormatBytes(afterSnapshot.TotalBytes)} across {FormatDeletedFileCount(afterSnapshot.ItemCount)}.";
        }

        return new CleanupTargetExecutionResult(
            target.Title,
            Succeeded: true,
            Skipped: false,
            DeletedFileCount: ClampDeletedFileCount(deletedItems),
            ReclaimedBytes: Math.Max(0, reclaimedBytes),
            message);
    }

    private static CleanupTargetExecutionResult CleanDirectoryTarget(
        string title,
        string rootPath,
        AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return new CleanupTargetExecutionResult(
                title,
                Succeeded: true,
                Skipped: true,
                DeletedFileCount: 0,
                ReclaimedBytes: 0,
                "The cleanup folder is not available on this system.");
        }

        int deletedFiles = 0;
        long reclaimedBytes = 0;
        int excludedPaths = 0;
        int failedPaths = 0;
        Stack<string> pendingDirectories = new();
        List<string> visitedDirectories = [];
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            string currentDirectory = pendingDirectories.Pop();
            visitedDirectories.Add(currentDirectory);

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                failedPaths++;
                continue;
            }
            catch (IOException)
            {
                failedPaths++;
                continue;
            }

            foreach (string filePath in files)
            {
                if (IsExcluded(filePath, settings.CleanupExclusions))
                {
                    excludedPaths++;
                    continue;
                }

                try
                {
                    FileInfo info = new(filePath);
                    if (info.IsReadOnly)
                    {
                        info.IsReadOnly = false;
                    }

                    long fileLength = info.Length;
                    info.Delete();
                    deletedFiles++;
                    reclaimedBytes += fileLength;
                }
                catch (UnauthorizedAccessException)
                {
                    failedPaths++;
                }
                catch (IOException)
                {
                    failedPaths++;
                }
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                failedPaths++;
                continue;
            }
            catch (IOException)
            {
                failedPaths++;
                continue;
            }

            foreach (string childDirectory in directories)
            {
                pendingDirectories.Push(childDirectory);
            }
        }

        foreach (string directoryPath in visitedDirectories.OrderByDescending(path => path.Length))
        {
            if (string.Equals(directoryPath, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsExcluded(directoryPath, settings.CleanupExclusions))
            {
                continue;
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
                {
                    Directory.Delete(directoryPath, recursive: false);
                }
            }
            catch (UnauthorizedAccessException)
            {
                failedPaths++;
            }
            catch (IOException)
            {
                failedPaths++;
            }
        }

        string message = deletedFiles switch
        {
            > 0 => $"Removed {DataSizeFormatter.FormatBytes(reclaimedBytes)} across {(deletedFiles == 1 ? "1 file" : $"{deletedFiles:N0} files")}.",
            _ => "No deletable files were removed from this target."
        };

        if (excludedPaths > 0)
        {
            message += $" Excluded {excludedPaths:N0} path(s).";
        }

        if (failedPaths > 0)
        {
            message += $" Skipped {failedPaths:N0} protected or locked path(s).";
        }

        return new CleanupTargetExecutionResult(
            title,
            Succeeded: true,
            Skipped: false,
            DeletedFileCount: deletedFiles,
            ReclaimedBytes: reclaimedBytes,
            message);
    }

    private static bool IsExcluded(string path, IReadOnlyList<string> exclusionPatterns)
    {
        string normalizedPath = path.Replace('/', '\\');

        foreach (string pattern in exclusionPatterns)
        {
            string normalizedPattern = pattern.Replace('/', '\\');
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                continue;
            }

            if (Path.IsPathRooted(normalizedPattern))
            {
                if (normalizedPath.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (normalizedPath.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int ClampDeletedFileCount(long deletedItems)
    {
        if (deletedItems <= 0)
        {
            return 0;
        }

        return deletedItems >= int.MaxValue
            ? int.MaxValue
            : (int)deletedItems;
    }

    private static string FormatDeletedFileCount(long deletedItems)
    {
        long normalizedItems = Math.Max(0, deletedItems);
        return normalizedItems == 1
            ? "1 file"
            : $"{normalizedItems:N0} files";
    }
}
