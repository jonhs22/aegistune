using AegisTune.CleanupEngine;
using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class CleanupScannerTests
{
    [Fact]
    public async Task ScanAsync_UsesShellTelemetryForRecycleBinAndMarksItExecutable()
    {
        CleanupScanner scanner = new(new FakeRecycleBinShell(
            new RecycleBinSnapshot(
                IsAvailable: true,
                ItemCount: 3,
                TotalBytes: 4096)));

        CleanupScanResult result = await scanner.ScanAsync();
        CleanupTargetScanResult recycleBin = Assert.Single(result.Targets.Where(target => target.Title == "Recycle Bin"));

        Assert.Equal(CleanupTargetStatus.Ready, recycleBin.Status);
        Assert.True(recycleBin.SupportsExecution);
        Assert.Equal(3, recycleBin.FileCount);
        Assert.Equal(4096, recycleBin.ReclaimableBytes);
    }

    [Fact]
    public async Task ScanAsync_ReportsRecycleBinTelemetryFailure()
    {
        CleanupScanner scanner = new(new FakeRecycleBinShell(
            new RecycleBinSnapshot(
                IsAvailable: false,
                ItemCount: 0,
                TotalBytes: 0,
                Note: "Recycle Bin query failed: access denied.")));

        CleanupScanResult result = await scanner.ScanAsync();
        CleanupTargetScanResult recycleBin = Assert.Single(result.Targets.Where(target => target.Title == "Recycle Bin"));

        Assert.Equal(CleanupTargetStatus.Error, recycleBin.Status);
        Assert.False(recycleBin.SupportsExecution);
        Assert.Equal("Recycle Bin query failed: access denied.", recycleBin.Notes);
    }

    private sealed class FakeRecycleBinShell : IRecycleBinShell
    {
        private readonly RecycleBinSnapshot _snapshot;

        public FakeRecycleBinShell(RecycleBinSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public void Empty()
        {
        }

        public RecycleBinSnapshot Query() => _snapshot;
    }
}
