using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class StartupEntryRecordTests
{
    [Fact]
    public void EntryBrief_ContainsCoreExecutionEvidence()
    {
        StartupEntryRecord entry = new(
            "OneDrive",
            "\"C:\\Program Files\\Microsoft OneDrive\\OneDrive.exe\" /background",
            "Registry Run (64-bit)",
            "Current user",
            @"C:\Program Files\Microsoft OneDrive\OneDrive.exe",
            true,
            false,
            StartupImpactLevel.High,
            StartupEntryOrigin.RegistryValue,
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
            "OneDrive",
            "Registry64");

        Assert.True(entry.CanOpenResolvedTarget);
        Assert.True(entry.CanDisableFromStartup);
        Assert.Contains("Startup entry: OneDrive", entry.EntryBrief);
        Assert.Contains("Launch command:", entry.EntryBrief);
        Assert.Contains("Resolved target:", entry.EntryBrief);
    }

    [Fact]
    public void StartupFolderPath_IsResolvedForStartupFolderEntries()
    {
        StartupEntryRecord entry = new(
            "Legacy Helper",
            @"C:\Users\john\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Legacy Helper.lnk",
            "Startup folder",
            "Current user",
            @"C:\Users\john\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Legacy Helper.lnk",
            true,
            false,
            StartupImpactLevel.Review,
            StartupEntryOrigin.StartupFolderFile,
            null,
            null,
            null,
            @"C:\Users\john\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Legacy Helper.lnk",
            "Shortcut target resolution stays manual in the current milestone.");

        Assert.True(entry.HasStartupFolderPath);
        Assert.True(entry.CanDisableFromStartup);
        Assert.Equal(@"C:\Users\john\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup", entry.StartupFolderPath);
    }

    [Fact]
    public void OrphanedEntry_CannotUseDisableAction()
    {
        StartupEntryRecord entry = new(
            "Old Helper",
            "\"C:\\Missing\\Old Helper.exe\"",
            "Registry Run (64-bit)",
            "Current user",
            @"C:\Missing\Old Helper.exe",
            false,
            true,
            StartupImpactLevel.Review,
            StartupEntryOrigin.RegistryValue,
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
            "Old Helper",
            "Registry64");

        Assert.False(entry.CanDisableFromStartup);
        Assert.True(entry.CanRemoveSafely);
    }
}
