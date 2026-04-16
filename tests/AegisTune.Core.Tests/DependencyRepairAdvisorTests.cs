using AegisTune.Core;
using AegisTune.RepairEngine;

namespace AegisTune.Core.Tests;

public sealed class DependencyRepairAdvisorTests
{
    [Fact]
    public void BuildCandidates_MapsAdobeVisualCppRuntimeToOfficialGuidance()
    {
        DateTimeOffset now = new(2026, 4, 15, 21, 30, 0, TimeSpan.Zero);
        AppInventorySnapshot inventory = new(
            new[]
            {
                new InstalledApplicationRecord(
                    "Adobe Photoshop 2026",
                    "26.1",
                    "Adobe",
                    InstalledApplicationSource.DesktopRegistry,
                    "All users",
                    @"HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\Adobe Photoshop 2026",
                    @"C:\Program Files\Adobe\Adobe Photoshop 2026",
                    true,
                    "\"C:\\Program Files\\Adobe\\Adobe Photoshop 2026\\uninstall.exe\"",
                    @"C:\Program Files\Adobe\Adobe Photoshop 2026\uninstall.exe",
                    true,
                    null)
            },
            now);

        DependencyRepairSignal signal = new(
            "MSVCP140.dll",
            "Application Popup",
            "The program can't start because MSVCP140.dll is missing from your computer.",
            now,
            "Photoshop",
            @"C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe");

        RepairCandidateRecord candidate = Assert.Single(
            DependencyRepairAdvisor.BuildCandidates(inventory, new[] { signal }));

        Assert.Equal("Dependency", candidate.Category);
        Assert.Equal(RiskLevel.Review, candidate.RiskLevel);
        Assert.True(candidate.RequiresAdministrator);
        Assert.Contains("Microsoft Visual C++ Redistributable", candidate.ProposedAction);
        Assert.Contains("Adobe", candidate.ProposedAction);
        Assert.Contains("third-party DLL mirrors", candidate.ProposedAction);
        Assert.Equal("Adobe Photoshop 2026", candidate.RelatedApplicationLabel);
        Assert.Equal(@"C:\Program Files\Adobe\Adobe Photoshop 2026", candidate.InstallLocationLabel);
        Assert.Equal(@"C:\Program Files\Adobe\Adobe Photoshop 2026\uninstall.exe", candidate.UninstallTargetLabel);
        Assert.Equal("Latest supported Visual C++ Redistributable", candidate.OfficialResourceTitleLabel);
        Assert.Equal(new Uri("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170"), candidate.OfficialResourceUri);
    }

    [Fact]
    public void BuildCandidates_MapsUnknownVendorDllToVendorRepairOnly()
    {
        DateTimeOffset now = new(2026, 4, 15, 21, 45, 0, TimeSpan.Zero);
        AppInventorySnapshot inventory = new(
            new[]
            {
                new InstalledApplicationRecord(
                    "Contoso CAD",
                    "8.4",
                    "Contoso",
                    InstalledApplicationSource.DesktopRegistry,
                    "Current user",
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Contoso CAD",
                    @"C:\Apps\Contoso CAD",
                    true,
                    "\"C:\\Apps\\Contoso CAD\\uninstall.exe\"",
                    @"C:\Apps\Contoso CAD\uninstall.exe",
                    true,
                    null)
            },
            now);

        DependencyRepairSignal signal = new(
            "Qt6Core.dll",
            "Application Popup",
            "The code execution cannot proceed because Qt6Core.dll was not found.",
            now,
            "Contoso CAD",
            @"C:\Apps\Contoso CAD\cad.exe");

        RepairCandidateRecord candidate = Assert.Single(
            DependencyRepairAdvisor.BuildCandidates(inventory, new[] { signal }));

        Assert.Equal("Dependency", candidate.Category);
        Assert.Contains("official vendor installer", candidate.ProposedAction);
        Assert.DoesNotContain("dll-files", candidate.ProposedAction, StringComparison.OrdinalIgnoreCase);
        Assert.False(candidate.HasOfficialResource);
        Assert.Equal(@"C:\Apps\Contoso CAD", candidate.InstallLocationLabel);
        Assert.Equal(@"C:\Apps\Contoso CAD\uninstall.exe", candidate.UninstallTargetLabel);
    }

    [Fact]
    public void BuildCandidates_IgnoresGenericCrashModuleWithoutMissingEvidence()
    {
        DateTimeOffset now = new(2026, 4, 15, 22, 00, 0, TimeSpan.Zero);
        AppInventorySnapshot inventory = new(Array.Empty<InstalledApplicationRecord>(), now);
        DependencyRepairSignal signal = new(
            "KERNELBASE.dll",
            "Application Error",
            "Faulting module name: KERNELBASE.dll, Exception code: 0xe0434352",
            now,
            "SomeApp",
            @"C:\Apps\SomeApp\app.exe");

        Assert.Empty(DependencyRepairAdvisor.BuildCandidates(inventory, new[] { signal }));
    }

    [Fact]
    public void BuildManualCandidates_ParsesPastedDllErrorText()
    {
        DateTimeOffset now = new(2026, 4, 15, 22, 10, 0, TimeSpan.Zero);
        AppInventorySnapshot inventory = new(
            new[]
            {
                new InstalledApplicationRecord(
                    "Adobe Photoshop 2026",
                    "26.1",
                    "Adobe",
                    InstalledApplicationSource.DesktopRegistry,
                    "All users",
                    @"HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\Adobe Photoshop 2026",
                    @"C:\Program Files\Adobe\Adobe Photoshop 2026",
                    true,
                    "\"C:\\Program Files\\Adobe\\Adobe Photoshop 2026\\uninstall.exe\"",
                    @"C:\Program Files\Adobe\Adobe Photoshop 2026\uninstall.exe",
                    true,
                    null)
            },
            now);

        string rawInput = """
            The program can't start because MSVCP140.dll is missing from your computer.
            Try reinstalling the program to fix this problem.
            C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe
            """;

        RepairCandidateRecord candidate = Assert.Single(
            DependencyRepairAdvisor.BuildManualCandidates(inventory, rawInput, now));

        Assert.Equal("Dependency", candidate.Category);
        Assert.Equal("Dependency repair review: Adobe Photoshop 2026", candidate.Title);
        Assert.Contains("Microsoft Visual C++ Redistributable", candidate.ProposedAction);
        Assert.True(candidate.HasApplicationPath);
        Assert.Equal(@"C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe", candidate.ApplicationPathLabel);
        Assert.Equal("Open Microsoft VC++ runtime page", candidate.OfficialResourceLabelText);
    }

    [Fact]
    public void BuildManualCandidates_MapsSideBySideToVisualCppRuntime()
    {
        DateTimeOffset now = new(2026, 4, 15, 22, 15, 0, TimeSpan.Zero);
        AppInventorySnapshot inventory = new(Array.Empty<InstalledApplicationRecord>(), now);

        RepairCandidateRecord candidate = Assert.Single(
            DependencyRepairAdvisor.BuildManualCandidates(
                inventory,
                "The application has failed to start because its side-by-side configuration is incorrect.",
                now));

        Assert.Equal("Dependency", candidate.Category);
        Assert.Contains("Visual C++", candidate.ProposedAction);
        Assert.Equal("Latest supported Visual C++ Redistributable", candidate.OfficialResourceTitleLabel);
    }
}
