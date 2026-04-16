using AegisTune.Core;
using AegisTune.App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace AegisTune.App.Pages;

public sealed partial class DashboardPage : Page
{
    private const double MediumLayoutBreakpoint = 760;
    private const double WideLayoutBreakpoint = 1160;
    private string? _loadErrorMessage;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += DashboardPage_Loaded;
        SizeChanged += DashboardPage_SizeChanged;
    }

    public DashboardSnapshot? Snapshot { get; private set; }

    public IReadOnlyList<ModuleSnapshot> Modules => Snapshot?.Modules ?? Array.Empty<ModuleSnapshot>();

    public IReadOnlyList<RecentActivity> Activities => Snapshot?.Activities ?? Array.Empty<RecentActivity>();

    public IReadOnlyList<ModuleSnapshot> PriorityModules => Modules
        .Where(module => module.IssueCount > 0)
        .OrderByDescending(module => module.IssueCount)
        .ThenByDescending(module => module.RiskLevel)
        .Take(3)
        .ToArray();

    public string DeviceName => Snapshot?.Profile.DeviceName ?? "Collecting device profile";

    public string OperatingSystemLabel => Snapshot?.Profile.OperatingSystem ?? "Waiting for system inventory";

    public string BuildLabel => Snapshot?.Profile.BuildLabel ?? "Build unknown";

    public string AdminLabel => Snapshot?.Profile.AdministratorLabel ?? "Session unknown";

    public string SupportIdentityLabel => Snapshot?.Firmware.SupportIdentityLabel ?? "Firmware identity pending";

    public string SupportIdentitySourceLabel => Snapshot?.Firmware.SupportIdentitySourceLabel ?? "Waiting for firmware routing";

    public string BiosVersionLabel => Snapshot?.Firmware.BiosVersionLabel ?? "BIOS version unknown";

    public string BiosReleaseLabel => Snapshot?.Firmware.BiosReleaseDateLabel ?? "Release date unknown";

    public string BiosAgeLabel => Snapshot?.Firmware.BiosAgeLabel ?? "BIOS age unknown";

    public string FirmwareModeLabel => Snapshot?.Firmware.FirmwareModeLabel ?? "Firmware mode unknown";

    public string SecureBootLabel => Snapshot?.Firmware.SecureBootLabel ?? "Secure Boot unknown";

    public string BaseboardLabel => Snapshot?.Firmware.BoardIdentityLabel ?? "Baseboard identity pending";

    public string FirmwareRouteLabel => Snapshot?.Firmware.SupportRouteLabel ?? "Firmware route pending";

    public string FirmwareReadinessSummary => Snapshot?.Firmware.DashboardStatusLine ?? "Waiting for firmware inventory";

    public string DryRunLabel => Snapshot?.Settings.DryRunEnabled == true ? "Dry-run mode on" : "Execution enabled";

    public string DashboardContextLine => $"{DryRunLabel} • {AdminLabel}";

    public int TotalIssueCount => Snapshot?.TotalIssueCount ?? 0;

    public string ReadyModuleCountLabel => Snapshot?.ReadyModuleCount.ToString() ?? "--";

    public string ReviewModuleCountLabel => Snapshot?.ReviewModuleCount.ToString() ?? "--";

    public string SessionModeLabel => Snapshot?.Settings.DryRunEnabled == true ? "Preview-first" : "Action-ready";

    public ModuleSnapshot? PrimaryModule => PriorityModules.FirstOrDefault();

    public string HeroSummaryLine => PrimaryModule is null
        ? "Scan this PC, see what matters, and open only the safest next step without digging through Windows settings."
        : $"Start with {PrimaryModule.Title}. It currently has the clearest evidence-backed work queue for this session.";

    public string PrimaryActionLabel => PrimaryModule is null
        ? "Start cleanup review"
        : $"Review {PrimaryModule.Title}";

    public string PrimaryActionSummary => PrimaryModule is null
        ? "No urgent issues are blocking this session. Use cleanup or reports for a quick posture check."
        : $"{PrimaryModule.IssueCount:N0} issue(s) are waiting in {PrimaryModule.Title}.";

    public string WorkflowSummary => PriorityModules.Count == 0
        ? "Start with Cleaner or Reports if you want a quick posture check. No module currently stands out as urgent."
        : $"Start with {PriorityModules[0].Title}. It currently carries the heaviest review load in this session.";

    public AppUpdateState UpdateState => App.GetService<IAppUpdateService>().CurrentState;

    public string UpdateHeroLine => UpdateState.StatusLine;

    public string UpdateDetailLine => $"{UpdateState.GuidanceLine} Current build: {UpdateState.CurrentVersion}. Published build: {UpdateState.LatestVersionLabel}.";

    public string DashboardStatusMessage => _loadErrorMessage
        ?? "The dashboard now refreshes live cleanup, startup, device, and firmware inventory data from this machine.";

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAdaptiveLayout(ActualWidth);

        if (Snapshot is not null || _loadErrorMessage is not null)
        {
            return;
        }

        try
        {
            Snapshot = await App.GetService<IDashboardSnapshotService>().GetSnapshotAsync();
            await App.GetService<IAppUpdateService>().RefreshAsync(true);
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The dashboard could not finish its initial inventory refresh.";
            App.GetService<ILogger<DashboardPage>>().LogError(ex, "Dashboard page failed to load the snapshot.");
        }

        Bindings.Update();
    }

    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private void ApplyAdaptiveLayout(double width)
    {
        if (width >= WideLayoutBreakpoint)
        {
            ApplyWideLayout();
            return;
        }

        if (width >= MediumLayoutBreakpoint)
        {
            ApplyMediumLayout();
            return;
        }

        ApplyNarrowLayout();
    }

    private void ApplyWideLayout()
    {
        LayoutRoot.Padding = new Thickness(36, 28, 36, 40);

        HeroMachineColumn.Width = new GridLength(340);
        HeroMachineRow.Height = new GridLength(0);
        Grid.SetRow(CurrentMachineCard, 0);
        Grid.SetColumn(CurrentMachineCard, 1);

        MetricColumn2.Width = new GridLength(1, GridUnitType.Star);
        MetricColumn3.Width = new GridLength(1, GridUnitType.Star);
        MetricRow2.Height = new GridLength(0);
        MetricRow3.Height = new GridLength(0);
        Grid.SetRow(ModulesReadyCard, 0);
        Grid.SetColumn(ModulesReadyCard, 0);
        Grid.SetRow(ReviewSurfacesCard, 0);
        Grid.SetColumn(ReviewSurfacesCard, 1);
        Grid.SetRow(SessionModeCard, 0);
        Grid.SetColumn(SessionModeCard, 2);
    }

    private void ApplyMediumLayout()
    {
        LayoutRoot.Padding = new Thickness(28, 20, 28, 28);

        HeroMachineColumn.Width = new GridLength(0);
        HeroMachineRow.Height = GridLength.Auto;
        Grid.SetRow(CurrentMachineCard, 1);
        Grid.SetColumn(CurrentMachineCard, 0);

        MetricColumn2.Width = new GridLength(1, GridUnitType.Star);
        MetricColumn3.Width = new GridLength(0);
        MetricRow2.Height = GridLength.Auto;
        MetricRow3.Height = new GridLength(0);
        Grid.SetRow(ModulesReadyCard, 0);
        Grid.SetColumn(ModulesReadyCard, 0);
        Grid.SetRow(ReviewSurfacesCard, 0);
        Grid.SetColumn(ReviewSurfacesCard, 1);
        Grid.SetRow(SessionModeCard, 1);
        Grid.SetColumn(SessionModeCard, 0);
    }

    private void ApplyNarrowLayout()
    {
        LayoutRoot.Padding = new Thickness(20, 16, 20, 24);

        HeroMachineColumn.Width = new GridLength(0);
        HeroMachineRow.Height = GridLength.Auto;
        Grid.SetRow(CurrentMachineCard, 1);
        Grid.SetColumn(CurrentMachineCard, 0);

        MetricColumn2.Width = new GridLength(0);
        MetricColumn3.Width = new GridLength(0);
        MetricRow2.Height = GridLength.Auto;
        MetricRow3.Height = GridLength.Auto;
        Grid.SetRow(ModulesReadyCard, 0);
        Grid.SetColumn(ModulesReadyCard, 0);
        Grid.SetRow(ReviewSurfacesCard, 1);
        Grid.SetColumn(ReviewSurfacesCard, 0);
        Grid.SetRow(SessionModeCard, 2);
        Grid.SetColumn(SessionModeCard, 0);
    }

    private void OpenWorkflowStep_Click(object sender, RoutedEventArgs e)
    {
        if (TryResolveSection(sender, out AppSection section))
        {
            App.GetService<MainWindow>().NavigateToSection(section);
        }
    }

    private void StartGuidedReview_Click(object sender, RoutedEventArgs e)
    {
        AppSection targetSection = PrimaryModule?.Section ?? AppSection.Cleaner;
        App.GetService<MainWindow>().NavigateToSection(targetSection);
    }

    private void OpenReportsFromHero_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<MainWindow>().NavigateToSection(AppSection.Reports);
    }

    private async void CheckAppUpdates_Click(object sender, RoutedEventArgs e)
    {
        await App.GetService<IAppUpdateService>().RefreshAsync(false);
        Bindings.Update();
    }

    private void OpenUpdateAction_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateState.CanOpenPreferredUpdateUrl)
        {
            App.GetService<MainWindow>().NavigateToSection(AppSection.Settings);
            return;
        }

        LaunchExternal(UpdateState.PreferredUpdateUrl, null);
    }

    private async void OpenReleaseNotes_Click(object sender, RoutedEventArgs e)
    {
        await App.GetService<AppReleaseNotesDialogService>().ShowAsync(XamlRoot);
    }

    private void OpenModule_Click(object sender, RoutedEventArgs e)
    {
        if (TryResolveSection(sender, out AppSection section))
        {
            App.GetService<MainWindow>().NavigateToSection(section);
        }
    }

    private static bool TryResolveSection(object sender, out AppSection section)
    {
        section = AppSection.Dashboard;

        object? tag = (sender as FrameworkElement)?.Tag;
        if (tag is AppSection typedSection)
        {
            section = typedSection;
            return true;
        }

        if (tag is string text && Enum.TryParse(text, out AppSection parsedSection))
        {
            section = parsedSection;
            return true;
        }

        return false;
    }

    private void LaunchExternal(string fileName, string? arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            App.GetService<ILogger<DashboardPage>>().LogError(ex, "Dashboard external launch failed for {FileName}.", fileName);
        }
    }
}
