using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AegisTune.Core;
using AegisTune.RepairEngine;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AegisTune.App.Pages;

public sealed partial class RepairPage : Page
{
    private const double MediumLayoutBreakpoint = 920;
    private const double WideLayoutBreakpoint = 1320;
    private const string ScanAdvisoryScope = "Current repair scan advisory";
    private const string ManualAdvisoryScope = "Manual dependency advisory";
    private const string AppsSettingsUri = "ms-settings:appsfeatures";
    private const string ProgramsAndFeaturesCommand = "appwiz.cpl";

    private string? _actionStatusMessage;
    private string? _loadErrorMessage;
    private string? _manualInput;
    private string? _manualStatusMessage;
    private AppInventorySnapshot? _appInventory;
    private IReadOnlyList<RepairCandidateRecord> _manualCandidates = Array.Empty<RepairCandidateRecord>();
    private IReadOnlyList<UndoJournalEntry> _undoEntries = Array.Empty<UndoJournalEntry>();

    public RepairPage()
    {
        InitializeComponent();
        Loaded += RepairPage_Loaded;
        SizeChanged += RepairPage_SizeChanged;
    }

    public ModuleSnapshot? Module { get; private set; }

    public RepairAdvisoryExportResult? LastExport { get; private set; }

    public RegistryRepairExecutionResult? LastRegistryRepairResult { get; private set; }

    public RepairScanResult? ScanResult { get; private set; }

    public IReadOnlyList<RepairCandidateRecord> Candidates => ScanResult?.Candidates ?? Array.Empty<RepairCandidateRecord>();

    public IReadOnlyList<RepairCandidateRecord> ManualCandidates => _manualCandidates;

    public IReadOnlyList<UndoJournalEntry> UndoEntries => _undoEntries.Take(12).ToArray();

    public IReadOnlyList<RepairCandidateRecord> ActiveAdvisoryCandidates => _manualCandidates.Count > 0
        ? _manualCandidates
        : Candidates;

    public IReadOnlyList<RepairPlaybookItem> Playbook => RepairPlaybookCatalog.All;

    public IReadOnlyList<RepairResourceLink> OfficialResources => RepairResourceCatalog.All;

    public string ModuleSubtitle => Module?.Subtitle ?? "Evidence-based repair surface.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading repair posture.";

    public string ActionStatusLine => _actionStatusMessage
        ?? "Use the advisory cards to review registry and leftover packs, open official repair links, and jump to trusted Windows surfaces without running untrusted fixes. Use the undo list below for startup restore or registry rollback only when you have matching evidence.";

    public string CandidateCountLabel => ScanResult?.CandidateCount.ToString("N0") ?? "--";

    public bool HasManualInput => !string.IsNullOrWhiteSpace(_manualInput);

    public bool HasActiveAdvisory => ActiveAdvisoryCandidates.Count > 0;

    public bool HasExportDirectory => LastExport is not null;

    public bool HasRegistryRepairBackup => LastRegistryRepairResult?.HasBackupFile == true;

    public bool HasUndoEntries => UndoEntries.Count > 0;

    public string ActiveAdvisoryScopeLabel => _manualCandidates.Count > 0
        ? ManualAdvisoryScope
        : ScanResult is null
            ? "Repair advisory is not ready yet."
            : ScanAdvisoryScope;

    public string ManualStatusLine => _manualStatusMessage
        ?? "No manual dependency triage has been run yet.";

    public string ExportStatusLine => _actionStatusMessage
        ?? (LastExport is null
            ? "Copy or export the current advisory when you need a support-ready remediation brief."
            : $"Last repair advisory export completed at {LastExport.ExportedAtLabel}.");

    public string RegistryRepairStatusLine => LastRegistryRepairResult is null
        ? "No in-app registry repair pack has been executed yet."
        : $"{LastRegistryRepairResult.StatusLine} {LastRegistryRepairResult.GuidanceLine}";

    public string RegistryRepairBackupLabel => LastRegistryRepairResult?.BackupFileLabel
        ?? "No registry backup file created yet.";

    public string UndoCenterSummaryLine => _undoEntries.Count == 0
        ? "No restore-point, startup restore, or registry undo history has been recorded yet."
        : $"{_undoEntries.Count:N0} undo item(s) are stored locally. Use restore points through Windows System Restore, use startup restore only for recorded startup-disable entries, and use registry rollback only when a .reg backup is attached.";

    public string UndoJournalStoragePathLabel => $"Undo journal: {App.GetService<IUndoJournalStore>().StoragePath}";

    public string ExportDirectoryLabel => LastExport?.ExportDirectory ?? "No repair advisory export folder created yet.";

    public string JsonExportPathLabel => LastExport?.JsonPath ?? "Repair advisory JSON export not created yet.";

    public string MarkdownExportPathLabel => LastExport?.MarkdownPath ?? "Repair advisory Markdown export not created yet.";

    private async void RepairPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAdaptiveLayout(ActualWidth);

        if (Module is not null && ScanResult is not null)
        {
            return;
        }

        await ReloadAsync();
    }

    private void RepairPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private async Task ReloadAsync()
    {
        try
        {
            Task<DashboardSnapshot> snapshotTask = App.GetService<IDashboardSnapshotService>().GetSnapshotAsync();
            Task<RepairScanResult> repairTask = App.GetService<IRepairScanner>().ScanAsync();
            Task<AppInventorySnapshot> appInventoryTask = App.GetService<IInstalledApplicationInventoryService>().GetSnapshotAsync();
            Task<IReadOnlyList<UndoJournalEntry>> undoJournalTask = App.GetService<IUndoJournalStore>().LoadAsync();

            DashboardSnapshot snapshot = await snapshotTask;
            ScanResult = await repairTask;
            _appInventory = await appInventoryTask;
            _undoEntries = await undoJournalTask;
            Module = snapshot.GetModule(AppSection.Repair);
            _loadErrorMessage = null;
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The repair scan could not be completed.";
            App.GetService<ILogger<RepairPage>>().LogError(ex, "Repair page failed to load.");
        }

        Bindings.Update();
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
        LaneColumn1.Width = new GridLength(1, GridUnitType.Star);
        LaneColumn2.Width = new GridLength(1, GridUnitType.Star);
        LaneColumn3.Width = new GridLength(1, GridUnitType.Star);
        LaneRow1.Height = GridLength.Auto;
        LaneRow2.Height = new GridLength(0);
        LaneRow3.Height = new GridLength(0);

        Grid.SetRow(LaneQueueCard, 0);
        Grid.SetColumn(LaneQueueCard, 0);
        Grid.SetRow(LaneManualCard, 0);
        Grid.SetColumn(LaneManualCard, 1);
        Grid.SetRow(LaneUndoCard, 0);
        Grid.SetColumn(LaneUndoCard, 2);
    }

    private void ApplyMediumLayout()
    {
        LaneColumn1.Width = new GridLength(1, GridUnitType.Star);
        LaneColumn2.Width = new GridLength(1, GridUnitType.Star);
        LaneColumn3.Width = new GridLength(0);
        LaneRow1.Height = GridLength.Auto;
        LaneRow2.Height = GridLength.Auto;
        LaneRow3.Height = new GridLength(0);

        Grid.SetRow(LaneQueueCard, 0);
        Grid.SetColumn(LaneQueueCard, 0);
        Grid.SetRow(LaneManualCard, 0);
        Grid.SetColumn(LaneManualCard, 1);
        Grid.SetRow(LaneUndoCard, 1);
        Grid.SetColumn(LaneUndoCard, 0);
        Grid.SetColumnSpan(LaneUndoCard, 2);
    }

    private void ApplyNarrowLayout()
    {
        LaneColumn1.Width = new GridLength(1, GridUnitType.Star);
        LaneColumn2.Width = new GridLength(0);
        LaneColumn3.Width = new GridLength(0);
        LaneRow1.Height = GridLength.Auto;
        LaneRow2.Height = GridLength.Auto;
        LaneRow3.Height = GridLength.Auto;

        Grid.SetRow(LaneQueueCard, 0);
        Grid.SetColumn(LaneQueueCard, 0);
        Grid.SetColumnSpan(LaneQueueCard, 1);
        Grid.SetRow(LaneManualCard, 1);
        Grid.SetColumn(LaneManualCard, 0);
        Grid.SetColumnSpan(LaneManualCard, 1);
        Grid.SetRow(LaneUndoCard, 2);
        Grid.SetColumn(LaneUndoCard, 0);
        Grid.SetColumnSpan(LaneUndoCard, 1);
    }

    private async void RefreshRepairScan_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing repair evidence and advisory candidates.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Repair evidence and advisory candidates refreshed.";
        Bindings.Update();
    }

    private void ManualInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _manualInput = ManualInputTextBox.Text;
        _manualCandidates = Array.Empty<RepairCandidateRecord>();
        if (string.IsNullOrWhiteSpace(_manualInput))
        {
            _manualStatusMessage = "Manual triage is empty. Paste the exact DLL or SideBySide error text to classify it.";
        }
        else
        {
            _manualStatusMessage = "Ready to analyze the pasted dependency error locally.";
        }

        Bindings.Update();
    }

    private async void AnalyzeManualInput_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_manualInput))
        {
            _manualStatusMessage = "Paste a DLL or runtime error message first.";
            Bindings.Update();
            return;
        }

        _appInventory ??= await App.GetService<IInstalledApplicationInventoryService>().GetSnapshotAsync();
        _manualCandidates = DependencyRepairAdvisor.BuildManualCandidates(
            _appInventory,
            _manualInput,
            DateTimeOffset.Now);

        _manualStatusMessage = _manualCandidates.Count == 0
            ? "No recognized dependency pattern was found in the pasted text. Paste the exact missing-DLL or SideBySide error message."
            : $"{_manualCandidates.Count:N0} official remediation candidate(s) were generated from the pasted error text.";
        Bindings.Update();
    }

    private void CopyAdvisory_Click(object sender, RoutedEventArgs e)
    {
        if (!HasActiveAdvisory)
        {
            _actionStatusMessage = "No repair advisory is available to copy yet.";
            Bindings.Update();
            return;
        }

        CopyTextToClipboard(
            RepairAdvisoryDocumentFormatter.BuildClipboardText(BuildActiveAdvisoryRequest()),
            "Copied the active repair advisory to the clipboard.");
    }

    private async void ExportAdvisory_Click(object sender, RoutedEventArgs e)
    {
        if (!HasActiveAdvisory)
        {
            _actionStatusMessage = "No repair advisory is available to export yet.";
            Bindings.Update();
            return;
        }

        try
        {
            _actionStatusMessage = "Exporting the active repair advisory.";
            Bindings.Update();

            LastExport = await App.GetService<IRepairAdvisoryExportService>()
                .ExportAsync(BuildActiveAdvisoryRequest());
            _actionStatusMessage = "Exported the active repair advisory to JSON and Markdown.";
            await OpenExportFolderIfEnabledAsync(LastExport.ExportDirectory, "Opened the repair advisory export folder automatically.");
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The repair advisory could not be exported: {ex.Message}";
            App.GetService<ILogger<RepairPage>>().LogError(ex, "Repair advisory export failed.");
        }

        Bindings.Update();
    }

    private void OpenExportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LastExport is null || !Directory.Exists(LastExport.ExportDirectory))
        {
            _actionStatusMessage = "No repair advisory export folder is available yet.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", LastExport.ExportDirectory, "Opened the repair advisory export folder.");
    }

    private void ClearManualInput_Click(object sender, RoutedEventArgs e)
    {
        _manualInput = null;
        _manualCandidates = Array.Empty<RepairCandidateRecord>();
        _manualStatusMessage = "Manual dependency triage has been cleared.";
        ManualInputTextBox.Text = string.Empty;
        Bindings.Update();
    }

    private RepairAdvisoryExportRequest BuildActiveAdvisoryRequest()
    {
        IReadOnlyList<RepairCandidateRecord> advisoryCandidates = ActiveAdvisoryCandidates;
        IReadOnlyList<RepairResourceLink> officialResources = advisoryCandidates.Any(candidate =>
            string.Equals(candidate.Category, "Dependency", StringComparison.OrdinalIgnoreCase))
            ? OfficialResources
            : Array.Empty<RepairResourceLink>();

        return new RepairAdvisoryExportRequest(
            _manualCandidates.Count > 0 ? ManualAdvisoryScope : ScanAdvisoryScope,
            _manualCandidates.Count > 0
                ? DateTimeOffset.Now
                : ScanResult?.ScannedAt ?? DateTimeOffset.Now,
            _manualCandidates.Count > 0 ? ManualStatusLine : ModuleStatusLine,
            advisoryCandidates,
            officialResources,
            _manualCandidates.Count > 0 ? _manualInput : null);
    }

    private void OpenCandidateOfficialResource_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || candidate.OfficialResourceUri is null)
        {
            return;
        }

        LaunchExternal(candidate.OfficialResourceUri.AbsoluteUri, null, $"Opened the official repair link for {candidate.Title}.");
    }

    private void OpenCandidateInstallLocation_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || !candidate.InstallLocationExists || string.IsNullOrWhiteSpace(candidate.InstallLocation))
        {
            return;
        }

        LaunchExternal("explorer.exe", candidate.InstallLocation, $"Opened the app folder for {candidate.RelatedApplicationLabel}.");
    }

    private void OpenCandidateApplicationPath_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || !candidate.ApplicationPathExists || string.IsNullOrWhiteSpace(candidate.ApplicationPath))
        {
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{candidate.ApplicationPath}\"", $"Opened the recorded app path for {candidate.Title}.");
    }

    private void OpenCandidateUninstallTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || !candidate.UninstallTargetExists || string.IsNullOrWhiteSpace(candidate.UninstallTargetPath))
        {
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{candidate.UninstallTargetPath}\"", $"Opened the uninstall target for {candidate.RelatedApplicationLabel}.");
    }

    private void OpenCandidateResidueFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || !candidate.ResidueFolderExists || string.IsNullOrWhiteSpace(candidate.ResidueFolderPath))
        {
            return;
        }

        LaunchExternal("explorer.exe", candidate.ResidueFolderPath, $"Opened the leftover folder for {candidate.RelatedApplicationLabel}.");
    }

    private void OpenCandidateAppCleanupFlow_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || !candidate.CanOpenApplicationReviewFlow)
        {
            return;
        }

        App.GetService<IApplicationReviewHandoffService>().SetPendingRequest(
            new ApplicationReviewHandoffRequest(
                candidate.RelatedApplicationLabel,
                Publisher: null,
                RegistryKeyPath: candidate.SourceLocation,
                SourceSection: AppSection.Repair,
                Reason: candidate.EvidenceSummary,
                SuggestedAction: candidate.HasResidueFolderPath || candidate.HasUninstallCommand
                    ? "Review uninstall leftovers and run the app cleanup workflow."
                    : "Review the matched app evidence and uninstall posture."));

        App.GetService<MainWindow>().NavigateToSection(AppSection.Apps);
        _actionStatusMessage = $"Opened Apps & Uninstall with a repair handoff for {candidate.RelatedApplicationLabel}.";
        Bindings.Update();
    }

    private void CopyCandidateExecutionBrief_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate))
        {
            return;
        }

        CopyTextToClipboard(BuildCandidateExecutionBrief(candidate), $"Copied the repair execution brief for {candidate.Title}.");
    }

    private void OpenAppsSettings_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal(AppsSettingsUri, null, "Opened Apps > Installed apps.");

    private void OpenAppsReviewSurface_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<MainWindow>().NavigateToSection(AppSection.Apps);
        _actionStatusMessage = "Opened Apps & Uninstall.";
        Bindings.Update();
    }

    private void OpenProgramsAndFeatures_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal(ProgramsAndFeaturesCommand, null, "Opened Programs and Features.");

    private void OpenSafetyCenter_Click(object sender, RoutedEventArgs e)
    {
        App.GetService<MainWindow>().NavigateToSection(AppSection.Safety);
        _actionStatusMessage = "Opened Safety & Undo.";
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

    private async void RunCandidateRepairPack_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedCandidate(sender, out RepairCandidateRecord? candidate) || !candidate.CanExecuteInAppRepairPack)
        {
            return;
        }

        AppSettings settings = await App.GetService<ISettingsStore>().LoadAsync();
        string modeLabel = settings.DryRunEnabled ? "preview" : "live";

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = settings.DryRunEnabled ? "Preview registry repair pack" : "Run registry repair pack",
            PrimaryButtonText = settings.DryRunEnabled ? "Preview repair pack" : "Back up and run repair pack",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"AegisTune will use the '{candidate.RepairActionLabelText}' workflow for:{Environment.NewLine}{candidate.Title}{Environment.NewLine}{Environment.NewLine}Registry target:{Environment.NewLine}{candidate.RegistryPathLabel}{Environment.NewLine}{Environment.NewLine}Execution mode: {modeLabel}.{Environment.NewLine}{Environment.NewLine}{candidate.ProposedAction}"
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _actionStatusMessage = settings.DryRunEnabled
                ? $"Previewing the registry repair pack for {candidate.Title}."
                : $"Running the registry repair pack for {candidate.Title}.";
            Bindings.Update();

            LastRegistryRepairResult = await App.GetService<IRegistryRepairExecutionService>()
                .ExecuteAsync(candidate, settings.DryRunEnabled);
            _actionStatusMessage = LastRegistryRepairResult.StatusLine;

            if (LastRegistryRepairResult.Succeeded && !LastRegistryRepairResult.WasDryRun)
            {
                await ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The registry repair pack could not be completed: {ex.Message}";
            App.GetService<ILogger<RepairPage>>().LogError(ex, "Registry repair pack failed for {Candidate}.", candidate.Title);
        }

        Bindings.Update();
    }

    private void OpenRegistryRepairBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LastRegistryRepairResult is null || !LastRegistryRepairResult.HasBackupFile)
        {
            _actionStatusMessage = "No registry backup file is available yet.";
            Bindings.Update();
            return;
        }

        string backupFilePath = LastRegistryRepairResult.BackupFilePath!;
        if (!File.Exists(backupFilePath))
        {
            _actionStatusMessage = "The last registry backup file is no longer available.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{backupFilePath}\"", "Opened the last registry backup file.");
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
            AppSettings startupRestoreSettings = await App.GetService<ISettingsStore>().LoadAsync();
            string startupModeLabel = startupRestoreSettings.DryRunEnabled ? "preview" : "live";

            ContentDialog startupConfirmationDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = startupRestoreSettings.DryRunEnabled ? "Preview startup restore" : "Restore startup entry",
                PrimaryButtonText = startupRestoreSettings.DryRunEnabled ? "Preview restore" : "Restore startup entry",
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
                _actionStatusMessage = startupRestoreSettings.DryRunEnabled
                    ? $"Previewing startup restore for {entry.Title}."
                    : $"Restoring startup entry for {entry.Title}.";
                Bindings.Update();

                StartupEntryActionResult result = await App.GetService<IStartupEntryActionService>()
                    .RestoreDisabledEntryAsync(entry, startupRestoreSettings.DryRunEnabled);
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
                App.GetService<ILogger<RepairPage>>().LogError(ex, "Startup restore failed for {UndoEntry}.", entry.Title);
            }

            Bindings.Update();
            return;
        }

        if (!entry.CanRunRegistryRollback)
        {
            _actionStatusMessage = "This undo entry does not expose an in-app rollback action.";
            Bindings.Update();
            return;
        }

        AppSettings settings = await App.GetService<ISettingsStore>().LoadAsync();
        string modeLabel = settings.DryRunEnabled ? "preview" : "live";

            ContentDialog confirmationDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = settings.DryRunEnabled ? "Preview registry rollback" : "Run registry rollback",
                PrimaryButtonText = settings.DryRunEnabled ? "Preview rollback" : "Import backup",
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
            _actionStatusMessage = settings.DryRunEnabled
                ? $"Previewing rollback for {entry.Title}."
                : $"Running rollback for {entry.Title}.";
            Bindings.Update();

            RegistryRollbackExecutionResult result = await App.GetService<IRegistryRollbackService>()
                .RollbackAsync(entry, settings.DryRunEnabled);
            _actionStatusMessage = result.StatusLine;

            if (result.Succeeded && !result.WasDryRun)
            {
                await ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The rollback could not be completed: {ex.Message}";
            App.GetService<ILogger<RepairPage>>().LogError(ex, "Undo rollback failed for {UndoEntry}.", entry.Title);
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

    private static string BuildCandidateExecutionBrief(RepairCandidateRecord candidate)
    {
        List<string> lines =
        [
            $"Title: {candidate.Title}",
            $"Category: {candidate.Category}",
            $"Risk: {candidate.RiskLabel}",
            $"Elevation: {candidate.AdminRequirementLabel}",
            $"Evidence: {candidate.EvidenceSummary}",
            $"Proposed action: {candidate.ProposedAction}",
            $"Source: {candidate.SourceLocation}"
        ];

        if (candidate.HasRelatedApplication)
        {
            lines.Add($"Matched app: {candidate.RelatedApplicationLabel}");
        }

        if (candidate.HasApplicationPath)
        {
            lines.Add($"App path: {candidate.ApplicationPathLabel}");
        }

        if (candidate.HasInstallLocation)
        {
            lines.Add($"Install location: {candidate.InstallLocationLabel}");
        }

        if (candidate.HasUninstallCommand)
        {
            lines.Add($"Uninstall command: {candidate.UninstallCommandLabel}");
        }

        if (candidate.HasUninstallTargetPath)
        {
            lines.Add($"Uninstall target: {candidate.UninstallTargetLabel}");
        }

        if (candidate.HasResidueSummary)
        {
            lines.Add($"Leftover footprint: {candidate.ResidueSummaryLabel}");
        }

        if (candidate.HasResidueFolderPath)
        {
            lines.Add($"Primary leftover folder: {candidate.ResidueFolderPathLabel}");
        }

        if (candidate.CanExecuteInAppRepairPack)
        {
            lines.Add($"Registry repair pack: {candidate.RepairActionLabelText}");
            lines.Add($"Registry target: {candidate.RegistryPathLabel}");
        }

        if (candidate.HasOfficialResource)
        {
            lines.Add($"Official repair link: {candidate.OfficialResourceUri}");
        }

        return string.Join(Environment.NewLine, lines);
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

    private static bool TryGetTaggedCandidate(object sender, [NotNullWhen(true)] out RepairCandidateRecord? candidate)
    {
        candidate = (sender as FrameworkElement)?.Tag as RepairCandidateRecord;
        return candidate is not null;
    }

    private static bool TryGetTaggedUndoEntry(object sender, [NotNullWhen(true)] out UndoJournalEntry? entry)
    {
        entry = (sender as FrameworkElement)?.Tag as UndoJournalEntry;
        return entry is not null;
    }

    private async Task OpenExportFolderIfEnabledAsync(string? exportDirectory, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(exportDirectory) || !Directory.Exists(exportDirectory))
        {
            return;
        }

        AppSettings settings = await App.GetService<ISettingsStore>().LoadAsync();
        if (!settings.OpenExportFolderAfterExport)
        {
            return;
        }

        LaunchExternal("explorer.exe", exportDirectory, successMessage);
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
            App.GetService<ILogger<RepairPage>>().LogError(ex, "Repair page action failed for {FileName}.", fileName);
        }

        Bindings.Update();
    }
}
