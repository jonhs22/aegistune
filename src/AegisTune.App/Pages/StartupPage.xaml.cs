using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace AegisTune.App.Pages;

public sealed partial class StartupPage : Page
{
    private const double MediumLayoutBreakpoint = 760;
    private const double WideLayoutBreakpoint = 1120;
    private readonly HashSet<string> _selectedOrphanedEntryKeys = new(StringComparer.OrdinalIgnoreCase);
    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private AppSettings? _settings;

    public StartupPage()
    {
        InitializeComponent();
        Loaded += StartupPage_Loaded;
        SizeChanged += StartupPage_SizeChanged;
    }

    public ModuleSnapshot? Module { get; private set; }

    public StartupInventorySnapshot? Inventory { get; private set; }

    public IReadOnlyList<StartupEntryRecord> Entries =>
        (Inventory?.Entries ?? Array.Empty<StartupEntryRecord>())
            .Where(ShouldShowStartupEntry)
            .Take(24)
            .ToArray();

    public IReadOnlyList<StartupEntryRecord> OrphanedEntries =>
        AllOrphanedEntries.Take(12).ToArray();

    private IReadOnlyList<StartupEntryRecord> AllOrphanedEntries =>
        Inventory?.Entries.Where(entry => entry.CanRemoveSafely).ToArray()
        ?? Array.Empty<StartupEntryRecord>();

    public string ModuleSubtitle => Module?.Subtitle ?? "Startup scan, disable controls, and broken-entry cleanup.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading startup inventory posture.";

    public string ActionStatusLine => _actionStatusMessage
        ?? "Scan startup entries first. Then disable or remove only the items you have reviewed. Registry-based changes can require restore-point preflight when that safety rule is enabled in Settings.";

    public string CurrentUserStartupFolderLabel => Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    public string CommonStartupFolderLabel => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

    public string EntryCountLabel => Inventory?.EntryCount.ToString("N0") ?? "--";

    public string ActionableCountLabel => Inventory?.ActionableCount.ToString("N0") ?? "--";

    public string SafeRemovalCountLabel => AllOrphanedEntries.Count.ToString("N0");

    public string HeroSummaryLine => Inventory is null
        ? "Scanning startup items, launch impact, and stale background entries."
        : Inventory.ActionableCount > 0
            ? $"{Inventory.ActionableCount:N0} startup item(s) need review."
            : $"{Inventory.EntryCount:N0} startup item(s) found. No urgent startup issues are blocking this session.";

    public string VisibleEntriesLabel => Inventory is null
        ? "Collecting startup entries."
        : _settings?.PreferReviewFirstLists == true && Inventory.ActionableCount > 0
            ? "Review-first mode is active, so this list shows only startup items that still need attention."
        : Inventory.EntryCount > Entries.Count
            ? $"Showing the first {Entries.Count} entries from {Inventory.EntryCount:N0}."
            : "Showing the current startup entries.";

    public string SafeOrphanedSummaryLabel => Inventory is null
        ? "Collecting safe orphaned entries."
        : AllOrphanedEntries.Count > OrphanedEntries.Count
            ? $"Showing the first {OrphanedEntries.Count} safe orphaned entries from {AllOrphanedEntries.Count:N0}."
            : "Showing the current safe orphaned entries.";

    public string SelectedOrphanedCountLabel => _selectedOrphanedEntryKeys.Count == 0
        ? "No orphaned entries are currently selected for batch removal."
        : $"{_selectedOrphanedEntryKeys.Count:N0} orphaned entr{(_selectedOrphanedEntryKeys.Count == 1 ? "y is" : "ies are")} selected for batch removal.";

    public string QuickReviewSummary => AllOrphanedEntries.Count == 0
        ? "No verified stale startup entries are ready for removal."
        : "Verified stale startup entries are listed below for safe removal review.";

    public string StartupInventorySummary => Inventory is null
        ? "Collecting startup entries."
        : Entries.Count == 0
            ? "No startup entries are visible in the current review mode."
            : VisibleEntriesLabel;

    public bool HasSelectedOrphanedEntries => _selectedOrphanedEntryKeys.Count > 0;

    private async void StartupPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAdaptiveLayout(ActualWidth);

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
            Task<StartupInventorySnapshot> inventoryTask = App.GetService<IStartupInventoryService>().GetSnapshotAsync();
            Task<AppSettings> settingsTask = App.GetService<ISettingsStore>().LoadAsync();

            DashboardSnapshot snapshot = await snapshotTask;
            Inventory = await inventoryTask;
            _settings = await settingsTask;
            Module = snapshot.GetModule(AppSection.Startup);
            SyncSelectionWithInventory();
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The startup inventory could not be completed.";
            App.GetService<ILogger<StartupPage>>().LogError(ex, "Startup page failed to load.");
        }

        Bindings.Update();
    }

    private void StartupPage_SizeChanged(object sender, SizeChangedEventArgs e)
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

        OverviewColumn2.Width = new GridLength(0.9, GridUnitType.Star);
        OverviewRow2.Height = new GridLength(0);
        Grid.SetRow(CurrentPostureCard, 0);
        Grid.SetColumn(CurrentPostureCard, 0);
        Grid.SetRow(InventoryTotalsCard, 0);
        Grid.SetColumn(InventoryTotalsCard, 1);

        PostureMetricColumn2.Width = new GridLength(1, GridUnitType.Star);
        PostureMetricRow2.Height = GridLength.Auto;
        Grid.SetRow(NeedReviewMetricPanel, 0);
        Grid.SetColumn(NeedReviewMetricPanel, 1);
        Grid.SetRow(SelectionStatusTextBlock, 1);
        Grid.SetColumn(SelectionStatusTextBlock, 0);
        Grid.SetColumnSpan(SelectionStatusTextBlock, 2);
    }

    private void ApplyMediumLayout()
    {
        LayoutRoot.Padding = new Thickness(28, 20, 28, 28);

        OverviewColumn2.Width = new GridLength(0.85, GridUnitType.Star);
        OverviewRow2.Height = new GridLength(0);
        Grid.SetRow(CurrentPostureCard, 0);
        Grid.SetColumn(CurrentPostureCard, 0);
        Grid.SetRow(InventoryTotalsCard, 0);
        Grid.SetColumn(InventoryTotalsCard, 1);

        PostureMetricColumn2.Width = new GridLength(1, GridUnitType.Star);
        PostureMetricRow2.Height = GridLength.Auto;
        Grid.SetRow(NeedReviewMetricPanel, 0);
        Grid.SetColumn(NeedReviewMetricPanel, 1);
        Grid.SetRow(SelectionStatusTextBlock, 1);
        Grid.SetColumn(SelectionStatusTextBlock, 0);
        Grid.SetColumnSpan(SelectionStatusTextBlock, 2);
    }

    private void ApplyNarrowLayout()
    {
        LayoutRoot.Padding = new Thickness(20, 16, 20, 24);

        OverviewColumn2.Width = new GridLength(0);
        OverviewRow2.Height = GridLength.Auto;
        Grid.SetRow(CurrentPostureCard, 0);
        Grid.SetColumn(CurrentPostureCard, 0);
        Grid.SetRow(InventoryTotalsCard, 1);
        Grid.SetColumn(InventoryTotalsCard, 0);

        PostureMetricColumn2.Width = new GridLength(0);
        PostureMetricRow2.Height = GridLength.Auto;
        Grid.SetRow(NeedReviewMetricPanel, 1);
        Grid.SetColumn(NeedReviewMetricPanel, 0);
        Grid.SetRow(SelectionStatusTextBlock, 2);
        Grid.SetColumn(SelectionStatusTextBlock, 0);
        Grid.SetColumnSpan(SelectionStatusTextBlock, 1);
    }

    private async void RefreshStartup_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing startup inventory.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Startup inventory refreshed.";
        Bindings.Update();
    }

    private void OpenCurrentUserStartupFolder_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(CurrentUserStartupFolderLabel, "Opened the current-user startup folder.");

    private void OpenCommonStartupFolder_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(CommonStartupFolderLabel, "Opened the all-users startup folder.");

    private void SyncSelectionWithInventory()
    {
        HashSet<string> validKeys = AllOrphanedEntries
            .Select(entry => entry.SelectionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selectedOrphanedEntryKeys.IntersectWith(validKeys);
    }

    private void OrphanedEntrySelection_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string selectionKey } checkBox)
        {
            return;
        }

        checkBox.IsChecked = _selectedOrphanedEntryKeys.Contains(selectionKey);
    }

    private void OrphanedEntrySelection_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string selectionKey })
        {
            return;
        }

        _selectedOrphanedEntryKeys.Add(selectionKey);
        Bindings.Update();
    }

    private void OrphanedEntrySelection_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string selectionKey })
        {
            return;
        }

        _selectedOrphanedEntryKeys.Remove(selectionKey);
        Bindings.Update();
    }

    private void SelectAllOrphanedEntries_Click(object sender, RoutedEventArgs e)
    {
        foreach (StartupEntryRecord entry in AllOrphanedEntries)
        {
            _selectedOrphanedEntryKeys.Add(entry.SelectionKey);
        }

        _actionStatusMessage = "Selected all safe orphaned startup entries.";
        Bindings.Update();
    }

    private void ClearOrphanedSelection_Click(object sender, RoutedEventArgs e)
    {
        _selectedOrphanedEntryKeys.Clear();
        _actionStatusMessage = "Cleared the safe orphaned startup selection.";
        Bindings.Update();
    }

    private async void RemoveSelectedOrphanedEntries_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<StartupEntryRecord> selectedEntries = AllOrphanedEntries
            .Where(entry => _selectedOrphanedEntryKeys.Contains(entry.SelectionKey))
            .ToArray();

        if (selectedEntries.Count == 0)
        {
            _actionStatusMessage = "Select at least one safe orphaned startup entry first.";
            Bindings.Update();
            return;
        }

        string selectedNames = string.Join(
            Environment.NewLine,
            selectedEntries.Take(6).Select(entry => $"- {entry.Name}"));
        string overflowSuffix = selectedEntries.Count > 6
            ? $"{Environment.NewLine}- ...and {selectedEntries.Count - 6:N0} more"
            : string.Empty;

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Remove selected stale startup entries",
            PrimaryButtonText = "Remove selected entries",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"AegisTune verified that the selected entries point to missing targets.{Environment.NewLine}{Environment.NewLine}Selected entries:{Environment.NewLine}{selectedNames}{overflowSuffix}{Environment.NewLine}{Environment.NewLine}Remove these stale startup references?"
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _actionStatusMessage = $"Removing {selectedEntries.Count:N0} selected startup entr{(selectedEntries.Count == 1 ? "y" : "ies")}.";
        Bindings.Update();

        List<StartupEntryActionResult> results = [];
        IStartupEntryActionService actionService = App.GetService<IStartupEntryActionService>();

        foreach (StartupEntryRecord entry in selectedEntries)
        {
            results.Add(await actionService.RemoveOrphanedEntryAsync(entry));
        }

        int successCount = results.Count(result => result.Succeeded);
        int failureCount = results.Count - successCount;

        await ReloadAsync();
        _actionStatusMessage = failureCount == 0
            ? $"Removed {successCount:N0} stale startup entr{(successCount == 1 ? "y" : "ies")}. Review Safety & Undo for the recorded cleanup history."
            : $"Removed {successCount:N0} stale startup entr{(successCount == 1 ? "y" : "ies")} with {failureCount:N0} failure(s).";
        Bindings.Update();
    }

    private async void RemoveOrphanedEntry_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry))
        {
            return;
        }

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Remove stale startup entry",
            PrimaryButtonText = "Remove stale entry",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content = $"AegisTune verified that '{entry.Name}' points to a missing target.\n\nSource: {entry.SourceLocationLabel}\nTarget: {entry.ResolvedTargetLabel}\n\nRemove this stale startup reference?"
        };

        ContentDialogResult confirmation = await confirmationDialog.ShowAsync();
        if (confirmation != ContentDialogResult.Primary)
        {
            return;
        }

        StartupEntryActionResult result =
            await App.GetService<IStartupEntryActionService>().RemoveOrphanedEntryAsync(entry);
        _actionStatusMessage = result.Succeeded
            ? $"{result.Message} Review Safety & Undo for the recorded cleanup history."
            : result.Message;

        if (result.Succeeded)
        {
            await ReloadAsync();
        }
        else
        {
            Bindings.Update();
        }
    }

    private async void DisableStartupEntry_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry) || !entry.CanDisableFromStartup)
        {
            return;
        }

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Disable startup launch",
            PrimaryButtonText = "Disable from startup",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            Content = $"AegisTune will stop '{entry.Name}' from launching with Windows.{Environment.NewLine}{Environment.NewLine}Source: {entry.SourceLocationLabel}{Environment.NewLine}Target: {entry.ResolvedTargetLabel}{Environment.NewLine}{Environment.NewLine}For startup-folder items the file is moved out of the startup folder. For registry items the startup value is removed and backup metadata is saved.{Environment.NewLine}{Environment.NewLine}Disable this startup entry?"
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        StartupEntryActionResult result =
            await App.GetService<IStartupEntryActionService>().DisableEntryAsync(entry);
        _actionStatusMessage = result.Succeeded
            ? $"{result.Message} Review Safety & Undo for the recorded disable history."
            : result.Message;

        if (result.Succeeded)
        {
            await ReloadAsync();
        }
        else
        {
            Bindings.Update();
        }
    }

    private void OpenResolvedTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry) || !entry.CanOpenResolvedTarget || string.IsNullOrWhiteSpace(entry.ResolvedTargetPath))
        {
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{entry.ResolvedTargetPath}\"", $"Opened the resolved target for {entry.Name}.");
    }

    private void OpenStartupFolderForEntry_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry) || !entry.HasStartupFolderPath || string.IsNullOrWhiteSpace(entry.StartupFolderPath))
        {
            return;
        }

        OpenFolder(entry.StartupFolderPath, $"Opened the startup source folder for {entry.Name}.");
    }

    private void CopyLaunchCommand_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry))
        {
            return;
        }

        CopyTextToClipboard(entry.LaunchCommand, $"Copied the startup command for {entry.Name}.");
    }

    private void CopySourceLocation_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry))
        {
            return;
        }

        CopyTextToClipboard(entry.SourceLocationLabel, $"Copied the startup source location for {entry.Name}.");
    }

    private void CopyStartupBrief_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEntry(sender, out StartupEntryRecord? entry))
        {
            return;
        }

        CopyTextToClipboard(entry.EntryBrief, $"Copied the startup brief for {entry.Name}.");
    }

    private void OpenFolder(string? folderPath, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            _actionStatusMessage = "The requested startup folder is not available on this machine.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", folderPath, successMessage);
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
            App.GetService<ILogger<StartupPage>>().LogError(ex, "Startup page action failed for {FileName}.", fileName);
        }

        Bindings.Update();
    }

    private static bool TryGetTaggedEntry(object sender, [NotNullWhen(true)] out StartupEntryRecord? entry)
    {
        entry = (sender as FrameworkElement)?.Tag as StartupEntryRecord;
        return entry is not null;
    }

    private bool ShouldShowStartupEntry(StartupEntryRecord entry)
    {
        if (_settings?.PreferReviewFirstLists != true || Inventory?.ActionableCount == 0)
        {
            return true;
        }

        return entry.IsOrphaned || entry.ImpactLevel == StartupImpactLevel.High;
    }
}
