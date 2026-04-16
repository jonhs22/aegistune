using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AegisTune.App.Pages;

public sealed partial class SafetyPage : Page
{
    private string? _actionStatusMessage;
    private string? _loadErrorMessage;
    private IReadOnlyList<UndoJournalEntry> _undoEntries = Array.Empty<UndoJournalEntry>();
    private AppSettings? _settings;

    public SafetyPage()
    {
        InitializeComponent();
        Loaded += SafetyPage_Loaded;
    }

    public IReadOnlyList<UndoJournalEntry> UndoEntries => _undoEntries.Take(20).ToArray();

    public string ModuleStatusLine => _loadErrorMessage
        ?? "Review restore-point protection, local undo history, startup restore entries, and uninstall trails before retrying any risky driver, registry, or app action.";

    public string ActionStatusLine => _actionStatusMessage
        ?? "Use System Restore for restore-point entries, restore startup entries only from recorded disable history, and use registry rollback only for the exact .reg backup you want to import.";

    public string HeroSummaryLine => _undoEntries.Count == 0
        ? "Protection is configured, but no local undo items have been recorded yet for this session."
        : $"{_undoEntries.Count:N0} safety item(s) are recorded locally, including {RollbackReadyCount:N0} registry rollback candidate(s), {DriverInstallEntryCount:N0} driver install record(s), {ApplicationUninstallEntryCount:N0} app uninstall record(s), {StartupActionEntryCount:N0} startup action record(s), and {RestorePointEntryCount:N0} restore-point item(s).";

    public string UndoEntryCountLabel => _undoEntries.Count.ToString("N0");

    public int RollbackReadyCount => _undoEntries.Count(entry => entry.CanRunRegistryRollback);

    public string RollbackReadyCountLabel => RollbackReadyCount.ToString("N0");

    public int RestorePointEntryCount => _undoEntries.Count(entry => entry.Kind == UndoJournalEntryKind.RestorePoint);

    public string RestorePointEntryCountLabel => RestorePointEntryCount.ToString("N0");

    public int DriverInstallEntryCount => _undoEntries.Count(entry => entry.Kind == UndoJournalEntryKind.DriverInstall);

    public string DriverInstallEntryCountLabel => DriverInstallEntryCount.ToString("N0");

    public int ApplicationUninstallEntryCount => _undoEntries.Count(entry => entry.Kind == UndoJournalEntryKind.ApplicationUninstall);

    public string ApplicationUninstallEntryCountLabel => ApplicationUninstallEntryCount.ToString("N0");

    public int StartupActionEntryCount => _undoEntries.Count(entry =>
        entry.Kind is UndoJournalEntryKind.StartupDisable or UndoJournalEntryKind.StartupCleanup or UndoJournalEntryKind.StartupRestore);

    public string StartupActionEntryCountLabel => StartupActionEntryCount.ToString("N0");

    public string SessionSafetyModeLabel => _settings is null
        ? "Loading"
        : _settings.DryRunEnabled
            ? "Preview-first"
            : "Action-ready";

    public string RestorePointPolicyLabel => _settings is null
        ? "Loading restore-point policy."
        : _settings.CreateRestorePointBeforeFixes
            ? "Restore point preflight is required before driver, registry, startup restore, and uninstall changes."
            : "Restore point preflight is currently disabled in Settings.";

    public string SessionProtectionSummary => _settings is null
        ? "Loading session safety posture."
        : $"Dry-run mode is {(_settings.DryRunEnabled ? "on" : "off")} by default. Restore-point preflight is {(_settings.CreateRestorePointBeforeFixes ? "on" : "off")} for driver, registry, startup restore, and uninstall changes. Driver installs, app uninstalls, and startup changes now append to this local safety history.";

    public string UndoHistorySummary => _undoEntries.Count == 0
        ? "No local rollback trail has been recorded yet."
        : RollbackReadyCount == 1
            ? "1 entry can run an in-app registry rollback. Recorded startup disable entries can run startup restore, while uninstall entries stay as audit history only."
            : $"{RollbackReadyCount:N0} entries can run an in-app registry rollback. Recorded startup disable entries can run startup restore, while uninstall entries stay as audit history only.";

    public string UndoHistoryLeadLine => _undoEntries.Count == 0
        ? "When AegisTune creates restore points or registry backups, the newest entries will appear here."
        : "Newest safety and undo entries appear first. Review the recorded status and guidance before you run any rollback action.";

    public string UndoJournalStoragePathLabel => $"Undo journal: {App.GetService<IUndoJournalStore>().StoragePath}";

    private async void SafetyPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            return;
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            Task<AppSettings> settingsTask = App.GetService<ISettingsStore>().LoadAsync();
            Task<IReadOnlyList<UndoJournalEntry>> undoJournalTask = App.GetService<IUndoJournalStore>().LoadAsync();

            _settings = await settingsTask;
            _undoEntries = await undoJournalTask;
            _loadErrorMessage = null;
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The safety and undo history could not be loaded.";
            App.GetService<ILogger<SafetyPage>>().LogError(ex, "Safety page failed to load.");
        }

        Bindings.Update();
    }

    private async void RefreshSafetyHistory_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing safety history and session protection state.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Safety history refreshed.";
        Bindings.Update();
    }

    private void OpenSystemRestore_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("rstrui.exe", null, "Opened Windows System Restore.");

    private void OpenUndoJournalFolder_Click(object sender, RoutedEventArgs e)
    {
        string storagePath = App.GetService<IUndoJournalStore>().StoragePath;
        bool hasJournalFile = File.Exists(storagePath);
        string target = hasJournalFile
            ? $"/select,\"{storagePath}\""
            : $"\"{Path.GetDirectoryName(storagePath) ?? storagePath}\"";

        LaunchExternal("explorer.exe", target, "Opened the undo journal folder.");
    }

    private void OpenRepairCenter_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<MainWindow>().NavigateToSection(AppSection.Repair);
        _actionStatusMessage = "Opened Repair & Recovery.";
        Bindings.Update();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<MainWindow>().NavigateToSection(AppSection.Settings);
        _actionStatusMessage = "Opened Settings.";
        Bindings.Update();
    }

    private async void RunUndoEntry_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedUndoEntry(sender, out UndoJournalEntry? entry))
        {
            return;
        }

        if (entry.Kind == UndoJournalEntryKind.RestorePoint)
        {
            OpenSystemRestore_Click(sender, e);
            return;
        }

        if (entry.CanRunStartupRestore)
        {
            _settings ??= await App.GetService<ISettingsStore>().LoadAsync();
            string startupModeLabel = _settings.DryRunEnabled ? "preview" : "live";

            ContentDialog startupConfirmationDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = _settings.DryRunEnabled ? "Preview startup restore" : "Restore startup entry",
                PrimaryButtonText = _settings.DryRunEnabled ? "Preview restore" : "Restore startup entry",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                Content =
                    $"AegisTune will restore the disabled startup entry for:{Environment.NewLine}{entry.Title}{Environment.NewLine}{Environment.NewLine}Execution mode: {startupModeLabel}.{Environment.NewLine}{Environment.NewLine}Use this only when you want the app to launch with Windows again."
            };

            if (await startupConfirmationDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                _actionStatusMessage = _settings.DryRunEnabled
                    ? $"Previewing startup restore for {entry.Title}."
                    : $"Restoring startup entry for {entry.Title}.";
                Bindings.Update();

                StartupEntryActionResult result = await App.GetService<IStartupEntryActionService>()
                    .RestoreDisabledEntryAsync(entry, _settings.DryRunEnabled);
                _actionStatusMessage = string.IsNullOrWhiteSpace(result.GuidanceLine)
                    ? result.Message
                    : $"{result.Message} {result.GuidanceLine}";

                if (result.Succeeded && !result.WasDryRun)
                {
                    await ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                _actionStatusMessage = $"The startup restore could not be completed: {ex.Message}";
                App.GetService<ILogger<SafetyPage>>().LogError(ex, "Startup restore failed for {UndoEntry}.", entry.Title);
            }

            Bindings.Update();
            return;
        }

        if (!entry.CanRunRegistryRollback)
        {
            _actionStatusMessage = "This entry does not expose an in-app rollback action.";
            Bindings.Update();
            return;
        }

        _settings ??= await App.GetService<ISettingsStore>().LoadAsync();
        string modeLabel = _settings.DryRunEnabled ? "preview" : "live";

            ContentDialog confirmationDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = _settings.DryRunEnabled ? "Preview registry rollback" : "Run registry rollback",
                PrimaryButtonText = _settings.DryRunEnabled ? "Preview rollback" : "Import backup",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                Content =
                $"AegisTune will use the saved registry backup for:{Environment.NewLine}{entry.Title}{Environment.NewLine}{Environment.NewLine}Backup file:{Environment.NewLine}{entry.ArtifactLabel}{Environment.NewLine}{Environment.NewLine}Execution mode: {modeLabel}.{Environment.NewLine}{Environment.NewLine}This imports the saved .reg backup back into Windows."
            };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _actionStatusMessage = _settings.DryRunEnabled
                ? $"Previewing rollback for {entry.Title}."
                : $"Running rollback for {entry.Title}.";
            Bindings.Update();

            RegistryRollbackExecutionResult result = await App.GetService<IRegistryRollbackService>()
                .RollbackAsync(entry, _settings.DryRunEnabled);
            _actionStatusMessage = result.StatusLine;

            if (result.Succeeded && !result.WasDryRun)
            {
                await ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The rollback could not be completed: {ex.Message}";
            App.GetService<ILogger<SafetyPage>>().LogError(ex, "Undo rollback failed for {UndoEntry}.", entry.Title);
        }

        Bindings.Update();
    }

    private void OpenUndoArtifact_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedUndoEntry(sender, out UndoJournalEntry? entry) || !entry.CanOpenArtifact || string.IsNullOrWhiteSpace(entry.ArtifactPathToOpen))
        {
            return;
        }

        string artifactPath = entry.ArtifactPathToOpen!;
        if (!File.Exists(artifactPath) && !Directory.Exists(artifactPath))
        {
            _actionStatusMessage = "The related artifact is no longer available.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{artifactPath}\"", $"Opened the related artifact for {entry.Title}.");
    }

    private void CopyUndoEntrySummary_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedUndoEntry(sender, out UndoJournalEntry? entry))
        {
            return;
        }

        List<string> lines =
        [
            $"Title: {entry.Title}",
            $"Kind: {entry.KindLabel}",
            $"Occurred: {entry.OccurredAtLabel}",
            $"Status: {entry.StatusLine}",
            $"Guidance: {entry.GuidanceLine}"
        ];

        if (entry.RestorePointCreated)
        {
            lines.Add("Restore point: created");
        }

        if (entry.RestorePointReused)
        {
            lines.Add("Restore point: reused");
        }

        if (!string.IsNullOrWhiteSpace(entry.ArtifactLabel))
        {
            lines.Add(entry.ArtifactLabel);
        }

        if (!string.IsNullOrWhiteSpace(entry.TargetDetailLabel))
        {
            lines.Add(entry.TargetDetailLabel);
        }

        if (!string.IsNullOrWhiteSpace(entry.CommandLineSummary))
        {
            lines.Add(entry.CommandLineSummary);
        }

        CopyTextToClipboard(string.Join(Environment.NewLine, lines), $"Copied the undo summary for {entry.Title}.");
    }

    private void CopyTextToClipboard(string text, string successMessage)
    {
        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
        _actionStatusMessage = successMessage;
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

            _actionStatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The requested Windows surface could not be opened: {ex.Message}";
            App.GetService<ILogger<SafetyPage>>().LogError(ex, "Safety page action failed for {FileName}.", fileName);
        }

        Bindings.Update();
    }

    private static bool TryGetTaggedUndoEntry(object sender, [NotNullWhen(true)] out UndoJournalEntry? entry)
    {
        entry = (sender as FrameworkElement)?.Tag as UndoJournalEntry;
        return entry is not null;
    }
}
