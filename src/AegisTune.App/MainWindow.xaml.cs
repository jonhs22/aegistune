using AegisTune.App.Services;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using System.IO;

namespace AegisTune.App;

public sealed partial class MainWindow : Window
{
    private readonly AppNavigationService _navigationService;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<MainWindow> _logger;
    private bool _suppressNavigationSelection;

    public MainWindow(AppNavigationService navigationService, ISettingsStore settingsStore, ILogger<MainWindow> logger)
    {
        _navigationService = navigationService;
        _settingsStore = settingsStore;
        _logger = logger;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        AppWindow.Title = "AegisTune for Windows";
        AppWindow.Resize(new SizeInt32(1440, 920));
        ApplyWindowIcon();

        ContentFrame.Navigated += (_, _) => Bindings.Update();

        InitializeShellPreferencesAsync();
        NavigateToSection(AppSection.Dashboard);
    }

    public void ApplySettings(AppSettings settings)
    {
        NavView.PaneDisplayMode = settings.PreferCompactNavigation
            ? NavigationViewPaneDisplayMode.LeftCompact
            : NavigationViewPaneDisplayMode.Left;
        NavView.IsPaneOpen = !settings.PreferCompactNavigation;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavigationSelection)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            NavigateTo(AppSection.Settings);
            return;
        }

        if (args.SelectedItemContainer?.Tag is string tag
            && Enum.TryParse(tag, out AppSection section))
        {
            NavigateTo(section);
        }
    }

    public void NavigateToSection(AppSection section)
    {
        _suppressNavigationSelection = true;
        try
        {
            SelectNavigationItem(section);
        }
        finally
        {
            _suppressNavigationSelection = false;
        }

        NavigateTo(section);
    }

    private void NavigateTo(AppSection section)
    {
        Type pageType = _navigationService.GetPageType(section);
        if (ContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        AppTitleBar.Title = _navigationService.GetTitle(section);
        ContentFrame.Navigate(pageType);
        _logger.LogInformation("Navigated to {Section}.", section);
    }

    private void SelectNavigationItem(AppSection section)
    {
        if (section == AppSection.Settings)
        {
            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        NavigationViewItem? matchingItem = NavView.MenuItems
            .Concat(NavView.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, section.ToString(), StringComparison.OrdinalIgnoreCase));

        if (matchingItem is not null)
        {
            NavView.SelectedItem = matchingItem;
        }
    }

    private async void InitializeShellPreferencesAsync()
    {
        try
        {
            ApplySettings(await _settingsStore.LoadAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shell preferences could not be loaded.");
        }
    }

    private void ApplyWindowIcon()
    {
        string preferredPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        string fallbackPath = Path.Combine(AppContext.BaseDirectory, "AppIcon.ico");
        string? iconPath = File.Exists(preferredPath)
            ? preferredPath
            : File.Exists(fallbackPath)
                ? fallbackPath
                : null;

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            _logger.LogWarning("App icon file could not be resolved from the application directory.");
            return;
        }

        AppWindow.SetIcon(iconPath);
    }
}
