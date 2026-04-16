using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace AegisTune.App.Pages;

public sealed partial class ReportsPage : Page
{
    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private MaintenanceReportRecord? _selectedHistoryReport;

    public ReportsPage()
    {
        InitializeComponent();
        Loaded += ReportsPage_Loaded;
    }

    public MaintenanceReportRecord? CurrentReport { get; private set; }

    public IReadOnlyList<MaintenanceReportRecord> History { get; private set; } = Array.Empty<MaintenanceReportRecord>();

    public ReportExportResult? LastExport { get; private set; }

    public MaintenanceReportRecord? ActiveReport => _selectedHistoryReport ?? CurrentReport;

    public string ModuleSubtitle => "Action and evidence reporting surface.";

    public string ModuleStatusLine => _loadErrorMessage
        ?? (ActiveReport is null
            ? "Generating the current maintenance report."
            : _selectedHistoryReport is null
                ? $"Latest report generated for {ActiveReport.DeviceName} at {ActiveReport.GeneratedAtLabel}."
                : $"Reviewing the stored report for {ActiveReport.DeviceName} from {ActiveReport.GeneratedAtLabel}.");

    public string ExportStatusLine => _actionStatusMessage
        ?? (LastExport is null
            ? "Export the current report to JSON and Markdown when you need a support-ready artifact."
            : $"Last export completed at {LastExport.ExportedAtLabel}.");

    public IReadOnlyList<ReportModuleSummary> Modules => ActiveReport?.Modules ?? Array.Empty<ReportModuleSummary>();

    public string TotalIssueCountLabel => ActiveReport?.TotalIssueCount.ToString("N0") ?? "--";

    public string HistoryCountLabel => History.Count.ToString("N0");

    public string ActiveReportDeviceLabel => ActiveReport?.DeviceName ?? "No active report selected.";

    public string ActiveReportGeneratedAtLabel => ActiveReport?.GeneratedAtLabel ?? "--";

    public string ActiveReportOriginLabel => _selectedHistoryReport is null
        ? "Viewing the latest generated report."
        : "Viewing a report from history.";

    public string ExportButtonLabel => _selectedHistoryReport is null
        ? "Export current report"
        : "Export selected report";

    public string ExportDirectoryLabel => LastExport?.ExportDirectory ?? "No export directory created yet.";

    public string JsonExportPathLabel => LastExport?.JsonPath ?? "JSON export not created yet.";

    public string MarkdownExportPathLabel => LastExport?.MarkdownPath ?? "Markdown export not created yet.";

    public string ReportStoragePathLabel => App.GetService<IReportStore>().StoragePath;

    public bool HasExportDirectory => LastExport is not null;

    public bool HasHistorySelection => _selectedHistoryReport is not null;

    private async void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (CurrentReport is not null)
        {
            return;
        }

        await RefreshReportAsync();
    }

    private async Task RefreshReportAsync()
    {
        try
        {
            CurrentReport = await App.GetService<IReportGenerator>().GenerateAsync();
            History = await App.GetService<IReportStore>().LoadAsync();
            if (_selectedHistoryReport is not null)
            {
                _selectedHistoryReport = History.FirstOrDefault(report => report.Id == _selectedHistoryReport.Id);
            }
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The reporting surface could not generate the current report.";
            App.GetService<ILogger<ReportsPage>>().LogError(ex, "Reports page failed to load.");
        }

        Bindings.Update();
    }

    private async void RefreshReport_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing the current maintenance report.";
        Bindings.Update();
        await RefreshReportAsync();
        _actionStatusMessage = "Maintenance report refreshed and persisted.";
        Bindings.Update();
    }

    private async void ExportActiveReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ActiveReport is null)
            {
                await RefreshReportAsync();
            }

            MaintenanceReportRecord? reportToExport = ActiveReport;
            if (reportToExport is null)
            {
                _actionStatusMessage = "The active report is not available for export.";
                Bindings.Update();
                return;
            }

            _actionStatusMessage = _selectedHistoryReport is null
                ? "Exporting the current report."
                : "Exporting the selected stored report.";
            Bindings.Update();

            LastExport = await App.GetService<IReportExportService>().ExportAsync(reportToExport);
            _actionStatusMessage = _selectedHistoryReport is null
                ? "Exported the current report to JSON and Markdown."
                : "Exported the selected stored report to JSON and Markdown.";

            await OpenExportFolderIfEnabledAsync(LastExport.ExportDirectory, "Opened the report export folder automatically.");
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The active report could not be exported.";
            App.GetService<ILogger<ReportsPage>>().LogError(ex, "Reports page export failed.");
        }

        Bindings.Update();
    }

    private void OpenExportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LastExport is null || !Directory.Exists(LastExport.ExportDirectory))
        {
            _actionStatusMessage = "The export folder is not available yet.";
            Bindings.Update();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = LastExport.ExportDirectory,
            UseShellExecute = true
        });
    }

    private void UseLatestReport_Click(object sender, RoutedEventArgs e)
    {
        _selectedHistoryReport = null;
        _actionStatusMessage = "Switched the report view back to the latest generated report.";
        Bindings.Update();
    }

    private void ReviewHistoryReport_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedReport(sender, out MaintenanceReportRecord? report))
        {
            return;
        }

        _selectedHistoryReport = report;
        _actionStatusMessage = $"Loaded the stored report from {report.GeneratedAtLabel} for review.";
        Bindings.Update();
    }

    private async void ExportHistoryReport_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedReport(sender, out MaintenanceReportRecord? report))
        {
            return;
        }

        _selectedHistoryReport = report;
        Bindings.Update();
        await ExportActiveReportAsync(report, "Exporting the selected stored report.", "Exported the selected stored report to JSON and Markdown.");
    }

    private void OpenReportStorage_Click(object sender, RoutedEventArgs e)
    {
        string storagePath = App.GetService<IReportStore>().StoragePath;
        string? storageDirectory = Path.GetDirectoryName(storagePath);
        if (string.IsNullOrWhiteSpace(storageDirectory) || !Directory.Exists(storageDirectory))
        {
            _actionStatusMessage = "The report storage folder is not available yet.";
            Bindings.Update();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = storageDirectory,
            UseShellExecute = true
        });
    }

    private async Task ExportActiveReportAsync(
        MaintenanceReportRecord reportToExport,
        string startMessage,
        string successMessage)
    {
        try
        {
            _actionStatusMessage = startMessage;
            Bindings.Update();
            LastExport = await App.GetService<IReportExportService>().ExportAsync(reportToExport);
            _actionStatusMessage = successMessage;
            await OpenExportFolderIfEnabledAsync(LastExport.ExportDirectory, "Opened the report export folder automatically.");
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The selected report could not be exported.";
            App.GetService<ILogger<ReportsPage>>().LogError(ex, "Reports page export failed.");
        }

        Bindings.Update();
    }

    private static bool TryGetTaggedReport(object sender, [NotNullWhen(true)] out MaintenanceReportRecord? report)
    {
        report = (sender as FrameworkElement)?.Tag as MaintenanceReportRecord;
        return report is not null;
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

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = exportDirectory,
            UseShellExecute = true
        });

        _actionStatusMessage = successMessage;
    }
}
