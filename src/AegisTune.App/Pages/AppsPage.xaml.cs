using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace AegisTune.App.Pages;

public sealed partial class AppsPage : Page
{
    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private AppSettings? _settings;
    private ApplicationReviewHandoffRequest? _pendingHandoffRequest;

    public AppsPage()
    {
        InitializeComponent();
        Loaded += AppsPage_Loaded;
    }

    public ModuleSnapshot? Module { get; private set; }

    public AppInventorySnapshot? Inventory { get; private set; }

    public IReadOnlyList<InstalledApplicationRecord> Applications => Inventory?.Applications.Take(32).ToArray() ?? Array.Empty<InstalledApplicationRecord>();

    public IReadOnlyList<InstalledApplicationRecord> ApplicationsToReview =>
        OrderForRepairHandoff(Inventory?.Applications.Where(app => app.NeedsLeftoverReview) ?? Array.Empty<InstalledApplicationRecord>())
            .Take(8)
            .ToArray();

    public IReadOnlyList<InstalledApplicationRecord> VisibleApplications =>
        OrderForRepairHandoff(Applications.Where(app => !app.NeedsLeftoverReview))
            .Take(GetVisibleApplicationLimit())
            .ToArray();

    public InstalledApplicationRecord? MatchedRepairHandoffApplication => _pendingHandoffRequest is null || Inventory is null
        ? null
        : Inventory.Applications.FirstOrDefault(_pendingHandoffRequest.Matches);

    public string ModuleSubtitle => Module?.Subtitle ?? "Application inventory surface.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading application inventory scope.";

    public string ActionStatusLine => _actionStatusMessage
        ?? (HasRepairHandoff
            ? RepairHandoffSummaryLine
            : "Open install folders, leftover folders, or uninstall targets, launch the recorded uninstall workflow, or move confirmed leftover folders into AegisTune quarantine.");

    public string TotalAppCountLabel => Inventory?.ApplicationCount.ToString("N0") ?? "--";

    public string DesktopAppCountLabel => Inventory?.DesktopApplicationCount.ToString("N0") ?? "--";

    public string PackagedAppCountLabel => Inventory?.PackagedApplicationCount.ToString("N0") ?? "--";

    public string NeedsReviewCountLabel => Inventory?.LeftoverReviewCandidateCount.ToString("N0") ?? "--";

    public string ResidueFootprintCountLabel => Inventory?.FilesystemResidueCandidateCount.ToString("N0") ?? "--";

    public string HeroSummaryLine => Inventory is null
        ? "Scanning installed apps, uninstall targets, install paths, and leftover footprints."
        : HasRepairHandoff
            ? $"{RepairHandoffTargetLabel} was handed off from Repair & Recovery for app-level review."
        : Inventory.LeftoverReviewCandidateCount > 0
            ? $"{Inventory.LeftoverReviewCandidateCount:N0} uninstall leftover candidate(s) need review first."
            : $"{Inventory.ApplicationCount:N0} installed apps inventoried. No uninstall leftover issues are blocking this session.";

    public string VisibleApplicationsLabel => Inventory is null
        ? "Collecting installed applications."
        : _settings?.PreferReviewFirstLists == true && ApplicationsToReview.Count > 0
            ? "Review-first mode is active, so the full inventory stays hidden until the flagged app issues are cleared or review-first mode is disabled in Settings."
        : Inventory.ApplicationCount > Applications.Count
            ? $"Showing the first {Applications.Count} apps from {Inventory.ApplicationCount:N0}."
            : "Showing the current installed app inventory.";

    public string PriorityReviewSummary => ApplicationsToReview.Count == 0
        ? "No uninstall leftover evidence is currently flagged."
        : "Review these app entries first before removing leftovers or chasing uninstall problems.";

    public string InstalledAppsSummary => Inventory is null
        ? "Collecting installed applications."
        : _settings?.PreferReviewFirstLists == true && ApplicationsToReview.Count > 0
            ? "Healthy app inventory is temporarily hidden because review-first mode is active."
            : VisibleApplications.Count == 0
                ? "No installed apps are available in the current view."
                : VisibleApplicationsLabel;

    public string ResidueReviewSummary => ApplicationsToReview.Count == 0
        ? "No leftover folders or stale uninstall footprints are currently flagged."
        : "These entries expose stale uninstall registrations or leftover folders in install, ProgramData, or AppData locations.";

    public Visibility RepairHandoffVisibility => HasRepairHandoff ? Visibility.Visible : Visibility.Collapsed;

    public bool HasRepairHandoff => _pendingHandoffRequest is not null;

    public string RepairHandoffTargetLabel => MatchedRepairHandoffApplication?.DisplayName
        ?? _pendingHandoffRequest?.DisplayName
        ?? "The requested app";

    public string RepairHandoffSummaryLine => _pendingHandoffRequest is null
        ? "No repair handoff is active."
        : MatchedRepairHandoffApplication is null
            ? $"Repair & Recovery sent '{_pendingHandoffRequest.DisplayName}' here, but the current app inventory did not match it yet. Refresh the scan if the uninstall state changed outside AegisTune."
            : $"{_pendingHandoffRequest.SourceSectionLabel} sent '{MatchedRepairHandoffApplication.DisplayName}' here because {_pendingHandoffRequest.Reason}";

    public string RepairHandoffNextStepLine
    {
        get
        {
            if (_pendingHandoffRequest is null)
            {
                return "No repair handoff is active.";
            }

            if (MatchedRepairHandoffApplication is null)
            {
                return "Refresh the app scan, then review the matched app entry when it appears.";
            }

            if (MatchedRepairHandoffApplication.CanCleanConfirmedResidue)
            {
                return "Next step: use Clean confirmed leftovers or open the leftover folder on the matched app card.";
            }

            if (MatchedRepairHandoffApplication.CanRunUninstall)
            {
                return "Next step: use Run uninstall on the matched app card, then let AegisTune rescan for leftovers.";
            }

            return $"Next step: {_pendingHandoffRequest.SuggestedAction}";
        }
    }

    private async void AppsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (Module is not null && Inventory is not null)
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
            Task<AppInventorySnapshot> inventoryTask = App.GetService<IInstalledApplicationInventoryService>().GetSnapshotAsync();
            Task<AppSettings> settingsTask = App.GetService<ISettingsStore>().LoadAsync();

            DashboardSnapshot snapshot = await snapshotTask;
            Inventory = await inventoryTask;
            _settings = await settingsTask;
            _pendingHandoffRequest = App.GetService<IApplicationReviewHandoffService>().PeekPendingRequest();
            Module = snapshot.GetModule(AppSection.Apps);
            _loadErrorMessage = null;
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The installed app inventory could not be completed.";
            App.GetService<ILogger<AppsPage>>().LogError(ex, "Apps page failed to load.");
        }

        Bindings.Update();
    }

    private async void RefreshApps_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing installed application inventory.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Installed application inventory refreshed.";
        Bindings.Update();
    }

    private void OpenAppsSettings_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("ms-settings:appsfeatures", null, "Opened Apps > Installed apps.");

    private void OpenRepairSurface_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<MainWindow>().NavigateToSection(AppSection.Repair);
        _actionStatusMessage = "Opened Repair & Recovery.";
        Bindings.Update();
    }

    private void ClearRepairHandoff_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<IApplicationReviewHandoffService>().Clear();
        _pendingHandoffRequest = null;
        _actionStatusMessage = "Cleared the repair handoff from Apps & Uninstall.";
        Bindings.Update();
    }

    private void OpenProgramsAndFeatures_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("appwiz.cpl", null, "Opened Programs and Features.");

    private void OpenInstallLocation_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app) || !app.InstallLocationExists || string.IsNullOrWhiteSpace(app.InstallLocation))
        {
            return;
        }

        LaunchExternal("explorer.exe", app.InstallLocation, $"Opened the install folder for {app.DisplayName}.");
    }

    private void OpenUninstallTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app) || !app.UninstallTargetExists || string.IsNullOrWhiteSpace(app.ResolvedUninstallTargetPath))
        {
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{app.ResolvedUninstallTargetPath}\"", $"Opened the uninstall target for {app.DisplayName}.");
    }

    private void OpenResidueFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app) || !app.HasPrimaryResiduePath)
        {
            return;
        }

        LaunchExternal("explorer.exe", app.PrimaryResiduePathLabel, $"Opened the leftover folder for {app.DisplayName}.");
    }

    private async void RunUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app) || !app.CanRunUninstall)
        {
            return;
        }

        _settings ??= await App.GetService<ISettingsStore>().LoadAsync();
        string modeLabel = _settings.DryRunEnabled ? "preview" : "live";

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _settings.DryRunEnabled ? "Preview uninstall workflow" : "Run uninstall workflow",
            PrimaryButtonText = _settings.DryRunEnabled ? "Preview uninstall" : "Run uninstall",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"AegisTune will launch the recorded uninstall workflow for:{Environment.NewLine}{app.DisplayName}{Environment.NewLine}{Environment.NewLine}Execution mode: {modeLabel}.{Environment.NewLine}{Environment.NewLine}Uninstall command:{Environment.NewLine}{app.UninstallCommandLabel}{Environment.NewLine}{Environment.NewLine}Use this only when you want to remove the app through its recorded uninstall path."
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _actionStatusMessage = _settings.DryRunEnabled
                ? $"Previewing the uninstall workflow for {app.DisplayName}."
                : $"Launching the uninstall workflow for {app.DisplayName}.";
            Bindings.Update();

            ApplicationUninstallExecutionResult result = await App.GetService<IApplicationUninstallService>()
                .UninstallAsync(app, _settings.DryRunEnabled);
            _actionStatusMessage = string.IsNullOrWhiteSpace(result.GuidanceLine)
                ? result.StatusLine
                : $"{result.StatusLine} {result.GuidanceLine}";

            if (result.Succeeded && !result.WasDryRun && result.CompletedWithinProbeWindow)
            {
                await ReloadAsync();

                InstalledApplicationRecord? residueFollowUp = FindConfirmedResidueFollowUp(app);
                if (residueFollowUp is not null)
                {
                    await PromptForPostUninstallCleanupAsync(app.DisplayName, residueFollowUp);
                }
                else
                {
                    ClearRepairHandoffIfMatched(app);
                    _actionStatusMessage = $"{result.StatusLine} Rescan completed and no confirmed leftover folders for {app.DisplayName} remain in the current app inventory.";
                    Bindings.Update();
                }
            }
            else
            {
                Bindings.Update();
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The uninstall workflow could not be completed: {ex.Message}";
            App.GetService<ILogger<AppsPage>>().LogError(ex, "Apps uninstall failed for {DisplayName}.", app.DisplayName);
            Bindings.Update();
        }
    }

    private async void RunConfirmedResidueCleanup_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app) || !app.CanCleanConfirmedResidue)
        {
            return;
        }

        _settings ??= await App.GetService<ISettingsStore>().LoadAsync();
        string modeLabel = _settings.DryRunEnabled ? "preview" : "live";

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _settings.DryRunEnabled ? "Preview leftover cleanup" : "Clean confirmed leftovers",
            PrimaryButtonText = _settings.DryRunEnabled ? "Preview cleanup" : "Move leftovers to quarantine",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"AegisTune will use only the leftover folders already confirmed in this scan for:{Environment.NewLine}{app.DisplayName}{Environment.NewLine}{Environment.NewLine}Execution mode: {modeLabel}.{Environment.NewLine}{Environment.NewLine}Confirmed residue:{Environment.NewLine}{app.FilesystemResiduePreview}{Environment.NewLine}{Environment.NewLine}This does not sweep beyond the listed residue paths."
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await ExecuteConfirmedResidueCleanupAsync(app, false);
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The leftover cleanup workflow could not be completed: {ex.Message}";
            App.GetService<ILogger<AppsPage>>().LogError(ex, "Apps leftover cleanup failed for {DisplayName}.", app.DisplayName);
            Bindings.Update();
        }
    }

    private void CopyUninstallCommand_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app) || !app.HasUninstallCommand)
        {
            return;
        }

        CopyTextToClipboard(app.UninstallCommandLabel, $"Copied the uninstall command for {app.DisplayName}.");
    }

    private void CopyAppBrief_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedApp(sender, out InstalledApplicationRecord? app))
        {
            return;
        }

        string brief = string.Join(
            Environment.NewLine,
            new[]
            {
                $"App: {app.DisplayName}",
                $"Source: {app.SourceLabel}",
                $"Version: {app.VersionLabel}",
                $"Publisher: {app.PublisherLabel}",
                $"Scope: {app.ScopeLabel}",
                $"Registry or package identity: {app.RegistryKeyPath}",
                $"Install location: {app.InstallLocationLabel}",
                $"Uninstall command: {app.UninstallCommandLabel}",
                $"Uninstall target: {app.UninstallTargetLabel}",
                $"Broken install evidence: {app.BrokenInstallEvidenceLabel}",
                $"Leftover footprint: {app.FilesystemResidueSummaryLabel}",
                $"Leftover paths: {app.FilesystemResiduePreview}",
                $"Estimated size: {app.EstimatedSizeLabel}"
            });

        CopyTextToClipboard(brief, $"Copied the app brief for {app.DisplayName}.");
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
            App.GetService<ILogger<AppsPage>>().LogError(ex, "Apps page action failed for {FileName}.", fileName);
        }

        Bindings.Update();
    }

    private static bool TryGetTaggedApp(object sender, [NotNullWhen(true)] out InstalledApplicationRecord? app)
    {
        app = (sender as FrameworkElement)?.Tag as InstalledApplicationRecord;
        return app is not null;
    }

    private InstalledApplicationRecord? FindConfirmedResidueFollowUp(InstalledApplicationRecord originalApp)
    {
        if (Inventory is null)
        {
            return null;
        }

        return Inventory.Applications.FirstOrDefault(app =>
                   app.CanCleanConfirmedResidue
                   && string.Equals(app.RegistryKeyPath, originalApp.RegistryKeyPath, StringComparison.OrdinalIgnoreCase))
               ?? Inventory.Applications.FirstOrDefault(app =>
                   app.CanCleanConfirmedResidue
                   && string.Equals(app.DisplayName, originalApp.DisplayName, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(app.Publisher, originalApp.Publisher, StringComparison.OrdinalIgnoreCase));
    }

    private async Task PromptForPostUninstallCleanupAsync(string originalDisplayName, InstalledApplicationRecord residueFollowUp)
    {
        _settings ??= await App.GetService<ISettingsStore>().LoadAsync();

        string primaryActionLabel = _settings.DryRunEnabled
            ? "Preview leftover cleanup"
            : "Clean leftovers now";
        string modeLabel = _settings.DryRunEnabled ? "preview" : "live";

        ContentDialog followUpDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Confirmed leftovers found after uninstall",
            PrimaryButtonText = primaryActionLabel,
            SecondaryButtonText = "Review later",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            Content =
                $"AegisTune rescanned Apps & Uninstall after the recorded uninstall workflow for:{Environment.NewLine}{originalDisplayName}{Environment.NewLine}{Environment.NewLine}Confirmed leftover folders are still present for:{Environment.NewLine}{residueFollowUp.DisplayName}{Environment.NewLine}{Environment.NewLine}Execution mode: {modeLabel}.{Environment.NewLine}{Environment.NewLine}Confirmed residue:{Environment.NewLine}{residueFollowUp.FilesystemResiduePreview}{Environment.NewLine}{Environment.NewLine}Run the confirmed cleanup now, or review the app entry first."
        };

        ContentDialogResult dialogResult = await followUpDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
        {
            _actionStatusMessage = $"Uninstall rescan found confirmed leftovers for {residueFollowUp.DisplayName}. Use Clean confirmed leftovers when you are ready.";
            Bindings.Update();
            return;
        }

        await ExecuteConfirmedResidueCleanupAsync(residueFollowUp, true);
    }

    private async Task ExecuteConfirmedResidueCleanupAsync(InstalledApplicationRecord app, bool launchedFromFollowUpPrompt)
    {
        _settings ??= await App.GetService<ISettingsStore>().LoadAsync();

        _actionStatusMessage = _settings.DryRunEnabled
            ? $"Previewing confirmed leftover cleanup for {app.DisplayName}."
            : $"Moving confirmed leftovers for {app.DisplayName} into quarantine.";
        Bindings.Update();

        ApplicationResidueCleanupExecutionResult result = await App.GetService<IApplicationResidueCleanupService>()
            .CleanupAsync(app, _settings.DryRunEnabled);

        string statusLine = string.IsNullOrWhiteSpace(result.GuidanceLine)
            ? result.StatusLine
            : $"{result.StatusLine} {result.GuidanceLine}";

        if (result.Succeeded && !result.WasDryRun)
        {
            await ReloadAsync();
            ClearRepairHandoffIfMatched(app);
            _actionStatusMessage = launchedFromFollowUpPrompt
                ? $"{result.StatusLine} {result.GuidanceLine} The post-uninstall leftover handoff is complete. Review Safety & Undo for the recorded cleanup history."
                : $"{result.StatusLine} {result.GuidanceLine} Review Safety & Undo for the recorded cleanup history.";
            Bindings.Update();
            return;
        }

        _actionStatusMessage = statusLine;
        Bindings.Update();
    }

    private int GetVisibleApplicationLimit()
    {
        if (_settings?.PreferReviewFirstLists == true && ApplicationsToReview.Count > 0)
        {
            return 0;
        }

        return 32;
    }

    private IEnumerable<InstalledApplicationRecord> OrderForRepairHandoff(IEnumerable<InstalledApplicationRecord> applications)
    {
        if (_pendingHandoffRequest is null)
        {
            return applications;
        }

        return applications.OrderByDescending(app => _pendingHandoffRequest.Matches(app));
    }

    private void ClearRepairHandoffIfMatched(InstalledApplicationRecord app)
    {
        if (_pendingHandoffRequest?.Matches(app) != true)
        {
            return;
        }

        App.GetService<IApplicationReviewHandoffService>().Clear();
        _pendingHandoffRequest = null;
    }
}
