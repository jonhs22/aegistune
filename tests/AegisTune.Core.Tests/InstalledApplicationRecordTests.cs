using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class InstalledApplicationRecordTests
{
    [Fact]
    public void ResidueEvidence_BuildsLeftoverSummaryAndReviewFlag()
    {
        InstalledApplicationRecord app = new(
            "Contoso Cleanup",
            "4.2",
            "Contoso",
            InstalledApplicationSource.DesktopRegistry,
            "Current user",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup",
            @"C:\Missing\Contoso Cleanup",
            false,
            "\"C:\\Missing\\ContosoCleanup\\uninstall.exe\"",
            @"C:\Missing\ContosoCleanup\uninstall.exe",
            false,
            512L * 1024 * 1024,
            [
                new ApplicationResidueRecord(
                    @"C:\Users\john\AppData\Local\Contoso Cleanup",
                    "Local AppData",
                    15L * 1024 * 1024,
                    42)
            ]);

        Assert.True(app.HasFilesystemResidue);
        Assert.True(app.NeedsLeftoverReview);
        Assert.Contains("leftover folder", app.FilesystemResidueSummaryLabel);
        Assert.Equal(@"C:\Users\john\AppData\Local\Contoso Cleanup", app.PrimaryResiduePathLabel);
        Assert.Contains("Local AppData", app.FilesystemResiduePreview);
    }

    [Fact]
    public void ResidueEvidence_DoesNotFlagHealthyInstalledApp()
    {
        InstalledApplicationRecord app = new(
            "Contoso CAD",
            "8.4",
            "Contoso",
            InstalledApplicationSource.DesktopRegistry,
            "Current user",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCAD",
            @"C:\Apps\Contoso CAD",
            true,
            "\"C:\\Apps\\Contoso CAD\\uninstall.exe\"",
            @"C:\Apps\Contoso CAD\uninstall.exe",
            true,
            null,
            [
                new ApplicationResidueRecord(
                    @"C:\Users\john\AppData\Local\Contoso CAD",
                    "Local AppData",
                    1024,
                    1)
            ]);

        Assert.True(app.HasFilesystemResidue);
        Assert.False(app.NeedsLeftoverReview);
    }
}
