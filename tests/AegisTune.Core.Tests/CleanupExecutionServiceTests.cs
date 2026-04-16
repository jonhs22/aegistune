using AegisTune.CleanupEngine;
using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class CleanupExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DeletesFilesAndRespectsExclusions()
    {
        string rootPath = CreateTemporaryDirectory();

        try
        {
            string removableFile = Path.Combine(rootPath, "remove.tmp");
            string excludedDirectory = Path.Combine(rootPath, "KeepFolder");
            string excludedFile = Path.Combine(excludedDirectory, "keep.tmp");
            Directory.CreateDirectory(excludedDirectory);
            await File.WriteAllTextAsync(removableFile, "remove-me");
            await File.WriteAllTextAsync(excludedFile, "keep-me");

            CleanupExecutionService service = new(userTempPath: rootPath, systemTempPath: rootPath);
            CleanupExecutionResult result = await service.ExecuteAsync(
                [
                    new CleanupTargetScanResult(
                        "User temp",
                        "Per-user temp files.",
                        rootPath,
                        CleanupTargetStatus.Ready,
                        EnabledByDefault: true,
                        FileCount: 2,
                        ReclaimableBytes: new FileInfo(removableFile).Length + new FileInfo(excludedFile).Length,
                        SupportsExecution: true)
                ],
                new AppSettings(
                    DryRunEnabled: false,
                    CleanupExclusionPatterns: "KeepFolder"));

            CleanupTargetExecutionResult targetResult = Assert.Single(result.Targets);
            Assert.True(targetResult.Succeeded);
            Assert.False(targetResult.Skipped);
            Assert.Equal(1, targetResult.DeletedFileCount);
            Assert.True(File.Exists(excludedFile));
            Assert.False(File.Exists(removableFile));
        }
        finally
        {
            DeleteTemporaryDirectory(rootPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InDryRunModeDoesNotDeleteFiles()
    {
        string rootPath = CreateTemporaryDirectory();

        try
        {
            string removableFile = Path.Combine(rootPath, "preview.tmp");
            await File.WriteAllTextAsync(removableFile, "preview-only");

            CleanupExecutionService service = new(userTempPath: rootPath, systemTempPath: rootPath);
            CleanupExecutionResult result = await service.ExecuteAsync(
                [
                    new CleanupTargetScanResult(
                        "User temp",
                        "Per-user temp files.",
                        rootPath,
                        CleanupTargetStatus.Ready,
                        EnabledByDefault: true,
                        FileCount: 1,
                        ReclaimableBytes: new FileInfo(removableFile).Length,
                        SupportsExecution: true)
                ],
                new AppSettings(DryRunEnabled: true));

            CleanupTargetExecutionResult targetResult = Assert.Single(result.Targets);
            Assert.True(targetResult.Skipped);
            Assert.True(File.Exists(removableFile));
            Assert.Contains("Dry-run mode is enabled", targetResult.Message);
        }
        finally
        {
            DeleteTemporaryDirectory(rootPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_EmptiesRecycleBinUsingShellTelemetry()
    {
        FakeRecycleBinShell recycleBinShell = new(
            new RecycleBinSnapshot(IsAvailable: true, ItemCount: 5, TotalBytes: 8192),
            new RecycleBinSnapshot(IsAvailable: true, ItemCount: 0, TotalBytes: 0));

        CleanupExecutionService service = new(recycleBinShell);
        CleanupExecutionResult result = await service.ExecuteAsync(
            [
                new CleanupTargetScanResult(
                    "Recycle Bin",
                    "Windows recycle bin.",
                    "Fixed drives",
                    CleanupTargetStatus.Ready,
                    EnabledByDefault: true,
                    FileCount: 5,
                    ReclaimableBytes: 8192,
                    SupportsExecution: true)
            ],
            new AppSettings(DryRunEnabled: false));

        CleanupTargetExecutionResult targetResult = Assert.Single(result.Targets);
        Assert.True(recycleBinShell.EmptyWasCalled);
        Assert.True(targetResult.Succeeded);
        Assert.False(targetResult.Skipped);
        Assert.Equal(5, targetResult.DeletedFileCount);
        Assert.Equal(8192, targetResult.ReclaimedBytes);
        Assert.Contains("Emptied the Recycle Bin", targetResult.Message);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsRecycleBinWhenItIsAlreadyEmptyAtExecutionTime()
    {
        FakeRecycleBinShell recycleBinShell = new(
            new RecycleBinSnapshot(IsAvailable: true, ItemCount: 0, TotalBytes: 0),
            new RecycleBinSnapshot(IsAvailable: true, ItemCount: 0, TotalBytes: 0));

        CleanupExecutionService service = new(recycleBinShell);
        CleanupExecutionResult result = await service.ExecuteAsync(
            [
                new CleanupTargetScanResult(
                    "Recycle Bin",
                    "Windows recycle bin.",
                    "Fixed drives",
                    CleanupTargetStatus.Ready,
                    EnabledByDefault: true,
                    FileCount: 3,
                    ReclaimableBytes: 4096,
                    SupportsExecution: true)
            ],
            new AppSettings(DryRunEnabled: false));

        CleanupTargetExecutionResult targetResult = Assert.Single(result.Targets);
        Assert.False(recycleBinShell.EmptyWasCalled);
        Assert.True(targetResult.Succeeded);
        Assert.True(targetResult.Skipped);
        Assert.Equal(0, targetResult.DeletedFileCount);
        Assert.Equal(0, targetResult.ReclaimedBytes);
        Assert.Contains("already empty", targetResult.Message);
    }

    private static string CreateTemporaryDirectory()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "AegisTune.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteTemporaryDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        Directory.Delete(directoryPath, recursive: true);
    }

    private sealed class FakeRecycleBinShell : IRecycleBinShell
    {
        private readonly Queue<RecycleBinSnapshot> _snapshots;

        public FakeRecycleBinShell(params RecycleBinSnapshot[] snapshots)
        {
            _snapshots = new Queue<RecycleBinSnapshot>(snapshots);
        }

        public bool EmptyWasCalled { get; private set; }

        public void Empty()
        {
            EmptyWasCalled = true;
        }

        public RecycleBinSnapshot Query()
        {
            if (_snapshots.Count == 0)
            {
                throw new InvalidOperationException("No more recycle bin snapshots were queued.");
            }

            return _snapshots.Dequeue();
        }
    }
}
