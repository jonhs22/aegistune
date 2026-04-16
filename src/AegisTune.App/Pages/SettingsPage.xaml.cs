using System.Diagnostics;
using AegisTune.Core;
using AegisTune.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AegisTune.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing;
    private AppSettings? _settings;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    public string StoragePathLabel => $"Settings file: {App.GetService<ISettingsStore>().StoragePath}";

    public string SaveStatusLabel { get; private set; } = "Settings not loaded yet.";

    public string SettingsSummaryLine => _settings is null
        ? "Loading local settings."
        : $"Dry-run {(_settings.DryRunEnabled ? "stays on" : "is disabled")} by default. Restore-point preflight {(_settings.CreateRestorePointBeforeFixes ? "protects driver and registry changes" : "is disabled")}. Review-first lists are {(_settings.PreferReviewFirstLists ? "active" : "disabled")}. App updates {(_settings.CheckForAppUpdatesOnLaunch ? "check on launch" : "stay manual only")}. Windows Health currently monitors {HealthScopeLabel}. Audio controls use {_settings.EffectiveAudioVolumeStepPercent:N0}% steps and flag defaults below {_settings.EffectiveAudioRecommendedVolumePercent:N0}%. Safety & Undo exposes the recorded restore and rollback history.";

    public AppUpdateState UpdateState => App.GetService<IAppUpdateService>().CurrentState;

    public string UpdateSettingsSummaryLabel => _settings is null
        ? "App update settings are not loaded yet."
        : _settings.CheckForAppUpdatesOnLaunch
            ? $"Launch checks are active. Feed URL: {_settings.EffectiveUpdateManifestUrl}"
            : $"Launch checks are off. Feed URL: {_settings.EffectiveUpdateManifestUrl}";

    public string UpdateFeedStatusLabel => $"{UpdateState.StatusLine} {UpdateState.GuidanceLine}".Trim();

    public string UpdateFeedDetailLabel => $"Current build: {UpdateState.CurrentVersion} • Published build: {UpdateState.LatestVersionLabel} • Distribution: {UpdateState.DistributionLabel} • Last checked: {UpdateState.CheckedAtLabel}";

    public string CleanupExclusionSummaryLabel => _settings is null
        ? "Cleanup exclusions not loaded yet."
        : _settings.CleanupExclusions.Count == 0
            ? "No cleanup exclusions are configured."
            : $"{_settings.CleanupExclusions.Count:N0} cleanup exclusion pattern(s) are active.";

    public string DriverRepositorySummaryLabel => _settings is null
        ? "Driver repositories not loaded yet."
        : _settings.DriverRepositoryRoots.Count == 0
            ? "No local driver repositories are configured."
            : $"{_settings.DriverRepositoryRoots.Count:N0} vetted driver repository root(s) are active.";

    public string ReviewDefaultsSummaryLabel => _settings is null
        ? "Review defaults are not loaded yet."
        : _settings.PreferReviewFirstLists
            ? $"Review-first mode is active. Registry and leftover review packs are {(_settings.IncludeRegistryResidueReview ? "enabled" : "disabled")}."
            : $"Full inventory lists stay visible by default. Registry and leftover review packs are {(_settings.IncludeRegistryResidueReview ? "enabled" : "disabled")}.";

    public string HealthReviewSummaryLabel => _settings is null
        ? "Windows Health scope is not loaded yet."
        : $"Active scope: {HealthScopeLabel}. Crash lookback: {_settings.EffectiveHealthCrashLookbackDays:N0} day(s). Windows Update lookback: {_settings.EffectiveHealthWindowsUpdateLookbackDays:N0} day(s).";

    public string AudioDefaultsSummaryLabel => _settings is null
        ? "Audio defaults are not loaded yet."
        : $"Audio controls step by {_settings.EffectiveAudioVolumeStepPercent:N0}% and mark default devices below {_settings.EffectiveAudioRecommendedVolumePercent:N0}% for review.";

    private string HealthScopeLabel => _settings is null
        ? "nothing yet"
        : BuildHealthScopeLabel(_settings);

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            return;
        }

        _isInitializing = true;
        ISettingsStore store = App.GetService<ISettingsStore>();
        _settings = await store.LoadAsync();
        ApplySettingsToControls(_settings);
        SaveStatusLabel = "Settings loaded from local storage.";
        _isInitializing = false;
        Bindings.Update();
    }

    private async void AnyToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        await SaveSettingsAsync("Saved toggle changes");
    }

    private async void AnySelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        await SaveSettingsAsync("Saved review scope changes");
    }

    private async void SaveCleanupExclusions_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync("Saved cleanup exclusions");
    }

    private async void ResetCleanupExclusions_Click(object sender, RoutedEventArgs e)
    {
        CleanupExclusionsBox.Text = string.Empty;
        await SaveSettingsAsync("Cleared cleanup exclusions");
    }

    private async void SaveDriverRepositories_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync("Saved driver repositories");
    }

    private async void SaveUpdateFeed_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync("Saved app update settings");
    }

    private async void CheckForUpdatesNow_Click(object sender, RoutedEventArgs e)
    {
        SaveStatusLabel = "Checking the configured update feed.";
        Bindings.Update();

        await SaveSettingsAsync("Saved app update settings");
        await App.GetService<IAppUpdateService>().RefreshAsync(false);
        SaveStatusLabel = "App update check completed.";
        Bindings.Update();
    }

    private void OpenUpdateFeed_Click(object sender, RoutedEventArgs e)
    {
        string feedUrl = App.GetService<IAppUpdateService>().CurrentState.FeedUrl;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            feedUrl = UpdateManifestUrlBox.Text.Trim();
        }

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            SaveStatusLabel = "No update feed URL is configured yet.";
            Bindings.Update();
            return;
        }

        LaunchExternal(feedUrl, null, "Opened the configured app update feed.");
    }

    private async void ViewReleaseNotes_Click(object sender, RoutedEventArgs e)
    {
        await App.GetService<AppReleaseNotesDialogService>().ShowAsync(XamlRoot);
    }

    private async void ResetDriverRepositories_Click(object sender, RoutedEventArgs e)
    {
        DriverRepositoryPathsBox.Text = string.Empty;
        await SaveSettingsAsync("Cleared driver repositories");
    }

    private async void AddDriverRepositoryFolder_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.GetService<MainWindow>()));

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            SaveStatusLabel = "Driver repository selection canceled.";
            Bindings.Update();
            return;
        }

        string[] paths = DriverRepositoryPathsBox.Text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!paths.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            DriverRepositoryPathsBox.Text = string.IsNullOrWhiteSpace(DriverRepositoryPathsBox.Text)
                ? folder.Path
                : $"{DriverRepositoryPathsBox.Text}{Environment.NewLine}{folder.Path}";
        }

        await SaveSettingsAsync("Added driver repository");
    }

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        string storagePath = App.GetService<ISettingsStore>().StoragePath;
        bool hasSettingsFile = File.Exists(storagePath);
        string target = hasSettingsFile
            ? $"/select,\"{storagePath}\""
            : $"\"{Path.GetDirectoryName(storagePath) ?? storagePath}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = target,
            UseShellExecute = true
        });

        SaveStatusLabel = "Opened the local settings folder.";
        Bindings.Update();
    }

    private async void ResetAllSettings_Click(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        AppSettings defaults = new();
        ApplySettingsToControls(defaults);
        _isInitializing = false;
        await SaveSettingsAsync("Reset settings to defaults");
    }

    private void ApplySettingsToControls(AppSettings settings)
    {
        DryRunToggle.IsOn = settings.DryRunEnabled;
        RestorePointToggle.IsOn = settings.CreateRestorePointBeforeFixes;
        UpdateChecksToggle.IsOn = settings.CheckForAppUpdatesOnLaunch;
        BrowserCleanupToggle.IsOn = settings.IncludeBrowserCleanup;
        OpenExportFolderToggle.IsOn = settings.OpenExportFolderAfterExport;
        CompactNavigationToggle.IsOn = settings.PreferCompactNavigation;
        UpdateManifestUrlBox.Text = settings.EffectiveUpdateManifestUrl;
        ReviewFirstListsToggle.IsOn = settings.PreferReviewFirstLists;
        RegistryResidueReviewToggle.IsOn = settings.IncludeRegistryResidueReview;
        CrashEvidenceToggle.IsOn = settings.IncludeCrashEvidenceInHealth;
        WindowsUpdateIssuesToggle.IsOn = settings.IncludeWindowsUpdateIssuesInHealth;
        ServiceReviewToggle.IsOn = settings.IncludeServiceReviewInHealth;
        ScheduledTaskReviewToggle.IsOn = settings.IncludeScheduledTaskReviewInHealth;
        CleanupExclusionsBox.Text = settings.CleanupExclusionPatterns;
        DriverRepositoryPathsBox.Text = settings.DriverRepositoryPaths;
        SelectComboBoxValue(HealthCrashLookbackComboBox, settings.EffectiveHealthCrashLookbackDays);
        SelectComboBoxValue(HealthWindowsUpdateLookbackComboBox, settings.EffectiveHealthWindowsUpdateLookbackDays);
        SelectComboBoxValue(AudioVolumeStepComboBox, settings.EffectiveAudioVolumeStepPercent);
        SelectComboBoxValue(AudioRecommendedVolumeComboBox, settings.EffectiveAudioRecommendedVolumePercent);
    }

    private async Task SaveSettingsAsync(string reason)
    {
        _settings = new AppSettings(
            DryRunToggle.IsOn,
            RestorePointToggle.IsOn,
            UpdateChecksToggle.IsOn,
            BrowserCleanupToggle.IsOn,
            CompactNavigationToggle.IsOn,
            UpdateManifestUrlBox.Text.Trim(),
            CleanupExclusionsBox.Text,
            DriverRepositoryPathsBox.Text,
            OpenExportFolderToggle.IsOn,
            ReviewFirstListsToggle.IsOn,
            RegistryResidueReviewToggle.IsOn,
            CrashEvidenceToggle.IsOn,
            WindowsUpdateIssuesToggle.IsOn,
            ServiceReviewToggle.IsOn,
            ScheduledTaskReviewToggle.IsOn,
            GetComboBoxInteger(HealthCrashLookbackComboBox, 7),
            GetComboBoxInteger(HealthWindowsUpdateLookbackComboBox, 14),
            GetComboBoxInteger(AudioVolumeStepComboBox, 10),
            GetComboBoxInteger(AudioRecommendedVolumeComboBox, 60));

        await App.GetService<ISettingsStore>().SaveAsync(_settings);
        App.GetService<MainWindow>().ApplySettings(_settings);
        SaveStatusLabel = $"{reason} at {DateTimeOffset.Now:HH:mm:ss}.";
        Bindings.Update();
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

            SaveStatusLabel = successMessage;
        }
        catch (Exception ex)
        {
            SaveStatusLabel = $"The requested update surface could not be opened: {ex.Message}";
        }

        Bindings.Update();
    }

    private static void SelectComboBoxValue(ComboBox comboBox, int value)
    {
        foreach (object item in comboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem
                && int.TryParse(comboBoxItem.Tag?.ToString(), out int itemValue)
                && itemValue == value)
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static int GetComboBoxInteger(ComboBox comboBox, int fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem comboBoxItem
            && int.TryParse(comboBoxItem.Tag?.ToString(), out int value))
        {
            return value;
        }

        return fallback;
    }

    private static string BuildHealthScopeLabel(AppSettings settings)
    {
        List<string> parts = [];
        if (settings.IncludeCrashEvidenceInHealth)
        {
            parts.Add("crashes");
        }

        if (settings.IncludeWindowsUpdateIssuesInHealth)
        {
            parts.Add("Windows Update");
        }

        if (settings.IncludeServiceReviewInHealth)
        {
            parts.Add("services");
        }

        if (settings.IncludeScheduledTaskReviewInHealth)
        {
            parts.Add("scheduled tasks");
        }

        return parts.Count == 0
            ? "no active health evidence sources"
            : string.Join(", ", parts);
    }
}
