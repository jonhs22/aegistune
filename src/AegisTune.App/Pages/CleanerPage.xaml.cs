using AegisTune.CleanupEngine;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AegisTune.App.Pages;

public sealed partial class CleanerPage : Page
{
    private const double MediumLayoutBreakpoint = 760;
    private const double WideLayoutBreakpoint = 1160;
    private readonly HashSet<string> _selectedTargetTitles = new(StringComparer.OrdinalIgnoreCase);
    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private AppSettings _settings = new();

    public CleanerPage()
    {
        InitializeComponent();
        Loaded += CleanerPage_Loaded;
        SizeChanged += CleanerPage_SizeChanged;
    }

    public ModuleSnapshot? Module { get; private set; }

    public CleanupScanResult? ScanResult { get; private set; }

    public CleanupExecutionResult? LastExecution { get; private set; }

    public IReadOnlyList<CleanupTargetScanResult> Targets => ScanResult?.Targets ?? Array.Empty<CleanupTargetScanResult>();

    public IReadOnlyList<CleanupTargetScanResult> SelectedTargets =>
        Targets.Where(target => _selectedTargetTitles.Contains(target.Title)).ToArray();

    public IReadOnlyList<CleanupTargetExecutionResult> ExecutionResults =>
        LastExecution?.Targets ?? Array.Empty<CleanupTargetExecutionResult>();

    public string ModuleSubtitle => Module?.Subtitle ?? "Preview-first cleanup surface.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading cleanup posture.";

    public string ActionStatusLine => _actionStatusMessage ?? "Review the guided cleanup defaults before you execute any deletion.";

    public string TotalReclaimableSizeLabel => ScanResult?.TotalBytesLabel ?? "Scanning...";

    public string TotalFileCountLabel => ScanResult?.TotalFileCountLabel ?? "Collecting file counts...";

    public string ActionableTargetCountLabel => ScanResult?.ActionableTargetCount.ToString("N0") ?? "--";

    public string HeroSummaryLine => ScanResult is null
        ? "Scanning temp files, recycle bin, and safe browser traces."
        : ScanResult.ActionableTargetCount > 0
            ? $"Found {ScanResult.TotalBytesLabel} of removable files across {ScanResult.ActionableTargetCount:N0} safe cleanup target(s)."
            : "No reclaimable safe cleanup targets were found in the current scan.";

    public string ScanSummary => ScanResult?.WarningMessage
        ?? (ScanResult is null
            ? "Scanning user temp, system temp, and recycle bin."
            : $"{ScanResult.ActionableTargetCount} target(s) currently expose {ScanResult.TotalBytesLabel} across {ScanResult.TotalFileCountLabel}.");

    public string SelectedTargetCountLabel => SelectedTargets.Count.ToString("N0");

    public bool HasSelectedTargets => SelectedTargets.Count > 0;

    public string SelectedTargetNamesLabel => SelectedTargets.Count == 0
        ? "No cleanup targets are selected."
        : string.Join(", ", SelectedTargets.Select(target => target.Title));

    public string GuidedCleanupModeLabel => _settings.DryRunEnabled
        ? "Dry-run mode is on. Guided cleanup will preview what would be removed."
        : "Dry-run mode is off. Guided cleanup will delete selected files after confirmation.";

    public string CleanupExclusionSummary => _settings.CleanupExclusions.Count == 0
        ? "No cleanup exclusions are active."
        : $"{_settings.CleanupExclusions.Count:N0} exclusion pattern(s) will be skipped during cleanup.";

    public string LastExecutionSummary => LastExecution?.SummaryLabel ?? "No guided cleanup has been executed in this session.";

    public string LastExecutionTimestampLabel => LastExecution is null
        ? "No execution timestamp recorded yet."
        : $"Last run: {LastExecution.ProcessedAtLabel}";

    private async void CleanerPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAdaptiveLayout(ActualWidth);

        if (Module is not null && ScanResult is not null)
        {
            return;
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            Task<DashboardSnapshot> snapshotTask = App.GetService<IDashboardSnapshotService>().GetSnapshotAsync();
            Task<CleanupScanResult> scanTask = App.GetService<ICleanupScanner>().ScanAsync();
            Task<AppSettings> settingsTask = App.GetService<ISettingsStore>().LoadAsync();

            DashboardSnapshot snapshot = await snapshotTask;
            ScanResult = await scanTask;
            _settings = await settingsTask;
            Module = snapshot.GetModule(AppSection.Cleaner);
            SyncSelectionWithTargets();
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The cleanup scan could not be completed.";
            App.GetService<ILogger<CleanerPage>>().LogError(ex, "Cleaner page failed to load.");
        }

        Bindings.Update();
    }

    private void CleanerPage_SizeChanged(object sender, SizeChangedEventArgs e)
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
        OverviewColumn2.Width = new GridLength(1, GridUnitType.Star);
        OverviewRow2.Height = new GridLength(0);
        Grid.SetRow(CurrentPostureCard, 0);
        Grid.SetColumn(CurrentPostureCard, 0);
        Grid.SetRow(LiveTotalsCard, 0);
        Grid.SetColumn(LiveTotalsCard, 1);
    }

    private void ApplyMediumLayout()
    {
        LayoutRoot.Padding = new Thickness(28, 20, 28, 28);
        OverviewColumn2.Width = new GridLength(1, GridUnitType.Star);
        OverviewRow2.Height = new GridLength(0);
        Grid.SetRow(CurrentPostureCard, 0);
        Grid.SetColumn(CurrentPostureCard, 0);
        Grid.SetRow(LiveTotalsCard, 0);
        Grid.SetColumn(LiveTotalsCard, 1);
    }

    private void ApplyNarrowLayout()
    {
        LayoutRoot.Padding = new Thickness(20, 16, 20, 24);
        OverviewColumn2.Width = new GridLength(0);
        OverviewRow2.Height = GridLength.Auto;
        Grid.SetRow(CurrentPostureCard, 0);
        Grid.SetColumn(CurrentPostureCard, 0);
        Grid.SetRow(LiveTotalsCard, 1);
        Grid.SetColumn(LiveTotalsCard, 0);
    }

    private void SyncSelectionWithTargets()
    {
        HashSet<string> validTitles = Targets
            .Select(target => target.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selectedTargetTitles.IntersectWith(validTitles);

        if (_selectedTargetTitles.Count > 0)
        {
            return;
        }

        foreach (CleanupTargetScanResult target in Targets.Where(target => target.CanExecute && target.EnabledByDefault))
        {
            _selectedTargetTitles.Add(target.Title);
        }
    }

    private async void RefreshPreview_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing cleanup preview.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Cleanup preview refreshed from the current temp and recycle bin scan.";
        Bindings.Update();
    }

    private void CleanupTargetSelection_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string title } checkBox)
        {
            return;
        }

        checkBox.IsChecked = _selectedTargetTitles.Contains(title);
    }

    private void CleanupTargetSelection_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string title })
        {
            return;
        }

        _selectedTargetTitles.Add(title);
        Bindings.Update();
    }

    private void CleanupTargetSelection_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string title })
        {
            return;
        }

        _selectedTargetTitles.Remove(title);
        Bindings.Update();
    }

    private void SelectAllCleanupTargets_Click(object sender, RoutedEventArgs e)
    {
        foreach (CleanupTargetScanResult target in Targets.Where(target => target.CanExecute))
        {
            _selectedTargetTitles.Add(target.Title);
        }

        _actionStatusMessage = "Selected every cleanup target that supports guided execution.";
        Bindings.Update();
    }

    private void ClearCleanupSelection_Click(object sender, RoutedEventArgs e)
    {
        _selectedTargetTitles.Clear();
        _actionStatusMessage = "Cleared the guided cleanup selection.";
        Bindings.Update();
    }

    private async void RunGuidedCleanup_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTargets.Count == 0)
        {
            _actionStatusMessage = "Select at least one executable cleanup target first.";
            Bindings.Update();
            return;
        }

        _settings = await App.GetService<ISettingsStore>().LoadAsync();
        string selectedTargetsSummary = string.Join(
            Environment.NewLine,
            SelectedTargets.Select(target => $"- {target.Title} ({target.FormattedReclaimableSize})"));
        string preface = _settings.DryRunEnabled
            ? "Dry-run mode is enabled. No files will be deleted during this pass."
            : "Dry-run mode is disabled. Selected files will be deleted immediately after confirmation.";

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Run guided cleanup",
            PrimaryButtonText = _settings.DryRunEnabled ? "Run dry-run preview" : "Run cleanup",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"{preface}{Environment.NewLine}{Environment.NewLine}Selected targets:{Environment.NewLine}{selectedTargetsSummary}{Environment.NewLine}{Environment.NewLine}Active exclusions: {_settings.CleanupExclusions.Count:N0}"
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _actionStatusMessage = _settings.DryRunEnabled
            ? "Running guided cleanup preview."
            : "Running guided cleanup.";
        Bindings.Update();

        LastExecution = await App.GetService<ICleanupExecutionService>().ExecuteAsync(SelectedTargets, _settings);
        await ReloadAsync();
        _actionStatusMessage = LastExecution.SummaryLabel;
        Bindings.Update();
    }
}
