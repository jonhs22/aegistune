using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AegisTune.App.Pages;

public sealed partial class HealthPage : Page
{
    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private WindowsHealthFocusGuidance _selectedFocus = WindowsHealthFocusGuidance.Empty;

    public HealthPage()
    {
        InitializeComponent();
        Loaded += HealthPage_Loaded;
    }

    public ModuleSnapshot? Module { get; private set; }

    public WindowsHealthSnapshot? Snapshot { get; private set; }

    public IReadOnlyList<WindowsHealthEventRecord> CrashEvents => Snapshot?.CrashEvents ?? Array.Empty<WindowsHealthEventRecord>();

    public IReadOnlyList<WindowsHealthEventRecord> WindowsUpdateEvents => Snapshot?.WindowsUpdateEvents ?? Array.Empty<WindowsHealthEventRecord>();

    public IReadOnlyList<ServiceReviewRecord> ServiceCandidates => Snapshot?.ServiceCandidates ?? Array.Empty<ServiceReviewRecord>();

    public IReadOnlyList<ScheduledTaskReviewRecord> ScheduledTaskCandidates => Snapshot?.ScheduledTaskCandidates ?? Array.Empty<ScheduledTaskReviewRecord>();

    public string ModuleSubtitle => Module?.Subtitle ?? "Windows health review surface.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading Windows health posture.";

    public string ActionStatusLine => _actionStatusMessage
        ?? "Open Windows Update, Services, Task Scheduler, or Event Viewer only after you review the flagged evidence.";

    public string SelectedHealthFocusTitle => _selectedFocus.Title;

    public string SelectedHealthFocusSummary => _selectedFocus.Summary;

    public string SelectedHealthFocusNextStep => _selectedFocus.NextStep;

    public string SelectedHealthPrimaryActionLabel => _selectedFocus.PrimaryActionLabel;

    public string SelectedHealthSecondaryActionLabel => _selectedFocus.SecondaryActionLabel;

    public Visibility SelectedHealthPrimaryActionVisibility => _selectedFocus.HasPrimaryAction ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectedHealthSecondaryActionVisibility => _selectedFocus.HasSecondaryAction ? Visibility.Visible : Visibility.Collapsed;

    public string HeroSummaryLine => Snapshot is null
        ? "Scanning recent crash evidence, Windows Update issues, services, and scheduled tasks."
        : Snapshot.IssueCount == 0
            ? "No issues are currently flagged within the active Windows health review scope."
            : $"{Snapshot.IssueCount:N0} health item(s) need review across crashes, Windows Update, services, and scheduled tasks.";

    public string IssueCountLabel => Snapshot?.IssueCount.ToString("N0") ?? "--";

    public string CrashCountLabel => Snapshot?.CrashCount.ToString("N0") ?? "--";

    public string WindowsUpdateIssueCountLabel => Snapshot?.WindowsUpdateIssueCount.ToString("N0") ?? "--";

    public string ServiceReviewCountLabel => Snapshot?.ServiceReviewCount.ToString("N0") ?? "--";

    public string ScheduledTaskReviewCountLabel => Snapshot?.ScheduledTaskReviewCount.ToString("N0") ?? "--";

    public string CrashSummary => CrashEvents.Count == 0
        ? "No recent application crash errors were detected in the current review window."
        : "Recent application crash evidence is listed below for review.";

    public string WindowsUpdateSummary => WindowsUpdateEvents.Count == 0
        ? "No recent Windows Update warning or error events were detected."
        : "Recent Windows Update warning or error events are listed below.";

    public string ServiceSummary => ServiceCandidates.Count == 0
        ? "No broken or stopped third-party automatic services are currently flagged."
        : "These services need review because the target is missing or the automatic service is not running.";

    public string ScheduledTaskSummary => ScheduledTaskCandidates.Count == 0
        ? "No scheduled tasks with broken action targets are currently flagged."
        : "These scheduled tasks need review because the configured action target is missing.";

    private async void HealthPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (Module is not null && Snapshot is not null)
        {
            return;
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            Task<DashboardSnapshot> dashboardTask = App.GetService<IDashboardSnapshotService>().GetSnapshotAsync();
            Task<WindowsHealthSnapshot> healthTask = App.GetService<IWindowsHealthService>().GetSnapshotAsync();

            DashboardSnapshot dashboard = await dashboardTask;
            Snapshot = await healthTask;
            Module = dashboard.GetModule(AppSection.Health);
            _loadErrorMessage = null;
            UpdateSelectedFocusAfterReload();
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The Windows health review could not be completed.";
            _selectedFocus = WindowsHealthFocusGuidance.Empty;
            App.GetService<ILogger<HealthPage>>().LogError(ex, "Health page failed to load.");
        }

        Bindings.Update();
    }

    private async void RefreshHealth_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing Windows health review.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Windows health review refreshed.";
        Bindings.Update();
    }

    private void OpenWindowsUpdate_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("ms-settings:windowsupdate", null, "Opened Windows Update settings.");

    private void OpenServices_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("services.msc", null, "Opened Services.");

    private void OpenTaskScheduler_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("taskschd.msc", null, "Opened Task Scheduler.");

    private void OpenEventViewer_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("eventvwr.msc", null, "Opened Event Viewer.");

    private void FocusCrashEvent_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEvent(sender, out WindowsHealthEventRecord? record))
        {
            return;
        }

        _selectedFocus = WindowsHealthFocusAdvisor.CreateCrash(record);
        _actionStatusMessage = $"Selected crash review for {record.Title}.";
        Bindings.Update();
    }

    private void FocusWindowsUpdateEvent_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEvent(sender, out WindowsHealthEventRecord? record))
        {
            return;
        }

        _selectedFocus = WindowsHealthFocusAdvisor.CreateWindowsUpdate(record);
        _actionStatusMessage = $"Selected Windows Update review for {record.Title}.";
        Bindings.Update();
    }

    private void FocusServiceReview_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedService(sender, out ServiceReviewRecord? service))
        {
            return;
        }

        _selectedFocus = WindowsHealthFocusAdvisor.CreateService(service);
        _actionStatusMessage = $"Selected service review for {service.DisplayTitle}.";
        Bindings.Update();
    }

    private void FocusScheduledTaskReview_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedTask(sender, out ScheduledTaskReviewRecord? task))
        {
            return;
        }

        _selectedFocus = WindowsHealthFocusAdvisor.CreateScheduledTask(task);
        _actionStatusMessage = $"Selected scheduled-task review for {task.TaskName}.";
        Bindings.Update();
    }

    private void RunSelectedHealthPrimaryAction_Click(object sender, RoutedEventArgs e) =>
        ExecuteSelectedHealthAction(_selectedFocus.PrimaryActionKind);

    private void RunSelectedHealthSecondaryAction_Click(object sender, RoutedEventArgs e) =>
        ExecuteSelectedHealthAction(_selectedFocus.SecondaryActionKind);

    private void OpenServiceExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedService(sender, out ServiceReviewRecord? service) || !service.ExecutablePathExists || string.IsNullOrWhiteSpace(service.ExecutablePath))
        {
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{service.ExecutablePath}\"", $"Opened the service target for {service.DisplayTitle}.");
    }

    private void OpenScheduledTaskExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedTask(sender, out ScheduledTaskReviewRecord? task) || !task.ExecutePathExists || string.IsNullOrWhiteSpace(task.ExecutePath))
        {
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{task.ExecutePath}\"", $"Opened the scheduled task target for {task.TaskName}.");
    }

    private void ExecuteSelectedHealthAction(WindowsHealthFocusActionKind actionKind)
    {
        switch (actionKind)
        {
            case WindowsHealthFocusActionKind.OpenEventViewer:
                OpenEventViewer_Click(this, new RoutedEventArgs());
                return;
            case WindowsHealthFocusActionKind.OpenWindowsUpdate:
                OpenWindowsUpdate_Click(this, new RoutedEventArgs());
                return;
            case WindowsHealthFocusActionKind.OpenRepair:
                App.GetService<MainWindow>().NavigateToSection(AppSection.Repair);
                _actionStatusMessage = "Opened Repair & Recovery.";
                Bindings.Update();
                return;
            case WindowsHealthFocusActionKind.OpenServices:
                OpenServices_Click(this, new RoutedEventArgs());
                return;
            case WindowsHealthFocusActionKind.OpenTaskScheduler:
                OpenTaskScheduler_Click(this, new RoutedEventArgs());
                return;
            case WindowsHealthFocusActionKind.OpenTarget when !string.IsNullOrWhiteSpace(_selectedFocus.TargetPath):
                LaunchExternal("explorer.exe", $"/select,\"{_selectedFocus.TargetPath}\"", "Opened the selected target.");
                return;
            default:
                _actionStatusMessage = "Select one issue first to enable the guided next action.";
                Bindings.Update();
                return;
        }
    }

    private void UpdateSelectedFocusAfterReload()
    {
        _selectedFocus = CrashEvents.Count > 0
            ? WindowsHealthFocusAdvisor.CreateCrash(CrashEvents[0])
            : WindowsUpdateEvents.Count > 0
                ? WindowsHealthFocusAdvisor.CreateWindowsUpdate(WindowsUpdateEvents[0])
                : ServiceCandidates.Count > 0
                    ? WindowsHealthFocusAdvisor.CreateService(ServiceCandidates[0])
                    : ScheduledTaskCandidates.Count > 0
                        ? WindowsHealthFocusAdvisor.CreateScheduledTask(ScheduledTaskCandidates[0])
                        : WindowsHealthFocusGuidance.Empty;
    }

    private void LaunchExternal(string fileName, string? arguments, string successMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            });

            _actionStatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The requested Windows surface could not be opened: {ex.Message}";
            App.GetService<ILogger<HealthPage>>().LogError(ex, "Health page action failed for {FileName}.", fileName);
        }

        Bindings.Update();
    }

    private static bool TryGetTaggedService(object sender, [NotNullWhen(true)] out ServiceReviewRecord? service)
    {
        service = (sender as FrameworkElement)?.Tag as ServiceReviewRecord;
        return service is not null;
    }

    private static bool TryGetTaggedTask(object sender, [NotNullWhen(true)] out ScheduledTaskReviewRecord? task)
    {
        task = (sender as FrameworkElement)?.Tag as ScheduledTaskReviewRecord;
        return task is not null;
    }

    private static bool TryGetTaggedEvent(object sender, [NotNullWhen(true)] out WindowsHealthEventRecord? record)
    {
        record = (sender as FrameworkElement)?.Tag as WindowsHealthEventRecord;
        return record is not null;
    }
}
