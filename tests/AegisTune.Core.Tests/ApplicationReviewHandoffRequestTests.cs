using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class ApplicationReviewHandoffRequestTests
{
    [Fact]
    public void Matches_PrefersRegistryKeyWhenAvailable()
    {
        ApplicationReviewHandoffRequest request = new(
            "Contoso Cleanup",
            null,
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup",
            AppSection.Repair,
            "stale uninstall evidence",
            "Review leftovers");

        InstalledApplicationRecord app = new(
            "Another Name",
            "1.0",
            "Contoso",
            InstalledApplicationSource.DesktopRegistry,
            "Current user",
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\ContosoCleanup",
            null,
            false,
            null,
            null,
            false,
            null);

        Assert.True(request.Matches(app));
    }

    [Fact]
    public void Matches_FallsBackToDisplayNameAndPublisher()
    {
        ApplicationReviewHandoffRequest request = new(
            "Contoso Cleanup",
            "Contoso Ltd.",
            null,
            AppSection.Repair,
            "leftover folders still present",
            "Clean confirmed leftovers");

        InstalledApplicationRecord app = new(
            "Contoso Cleanup",
            "1.0",
            "Contoso Ltd.",
            InstalledApplicationSource.DesktopRegistry,
            "Current user",
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Other",
            null,
            false,
            null,
            null,
            false,
            null);

        Assert.True(request.Matches(app));
    }
}
