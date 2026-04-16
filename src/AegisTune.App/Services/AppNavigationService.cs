using AegisTune.App.Pages;
using AegisTune.Core;

namespace AegisTune.App.Services;

public sealed class AppNavigationService
{
    private static readonly IReadOnlyDictionary<AppSection, (Type PageType, string Title)> Routes =
        new Dictionary<AppSection, (Type, string)>
        {
            [AppSection.Dashboard] = (typeof(DashboardPage), "Home"),
            [AppSection.Health] = (typeof(HealthPage), "Windows Health"),
            [AppSection.Audio] = (typeof(AudioPage), "Audio & Sound"),
            [AppSection.Cleaner] = (typeof(CleanerPage), "Cleanup"),
            [AppSection.Drivers] = (typeof(DriversPage), "Drivers & Firmware"),
            [AppSection.Startup] = (typeof(StartupPage), "Startup Review"),
            [AppSection.Apps] = (typeof(AppsPage), "Apps & Uninstall"),
            [AppSection.Repair] = (typeof(RepairPage), "Repair & Recovery"),
            [AppSection.Safety] = (typeof(SafetyPage), "Safety & Undo"),
            [AppSection.Reports] = (typeof(ReportsPage), "Reports"),
            [AppSection.Settings] = (typeof(SettingsPage), "Settings"),
            [AppSection.About] = (typeof(AboutPage), "About")
        };

    public Type GetPageType(AppSection section) => Routes[section].PageType;

    public string GetTitle(AppSection section) => Routes[section].Title;
}
