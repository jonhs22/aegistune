using System.Reflection;
using System.Diagnostics;
using AegisTune.Core;
using AegisTune.App.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace AegisTune.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    public string PublisherName => "ichiphost";

    public string SupportEmail => "info@ichiphost.gr";

    public string CreatorName => "John Papadakis";

    public string PackagePublisherSubject => "CN=ichiphost";

    public AppUpdateState UpdateState => App.GetService<IAppUpdateService>().CurrentState;

    public string DistributionLabel => UpdateState.DistributionLabel;

    public string UpdateSummaryLine => $"{UpdateState.StatusLine} {UpdateState.GuidanceLine}".Trim();

    public string UpdateDetailLine => $"Current build: {UpdateState.CurrentVersion} • Published build: {UpdateState.LatestVersionLabel} • Last checked: {UpdateState.CheckedAtLabel}";

    public string BuildVersion
    {
        get
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                Version? version = typeof(AboutPage).GetTypeInfo().Assembly.GetName().Version;
                return version is null
                    ? "Portable build"
                    : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
        }
    }

    private async void CheckUpdatesNow_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await App.GetService<IAppUpdateService>().RefreshAsync(false);
        Bindings.Update();
    }

    private void OpenUpdateSource_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (UpdateState.CanOpenPreferredUpdateUrl)
        {
            LaunchExternal(UpdateState.PreferredUpdateUrl);
            return;
        }

        App.GetService<MainWindow>().NavigateToSection(AppSection.Settings);
    }

    private async void OpenReleaseNotes_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await App.GetService<AppReleaseNotesDialogService>().ShowAsync(XamlRoot);
    }

    private static void LaunchExternal(string fileName)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true
        });
    }
}
