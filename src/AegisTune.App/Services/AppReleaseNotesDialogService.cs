using System.Diagnostics;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AegisTune.App.Services;

public sealed class AppReleaseNotesDialogService
{
    private readonly IAppUpdateService _appUpdateService;
    private readonly ILogger<AppReleaseNotesDialogService> _logger;

    public AppReleaseNotesDialogService(
        IAppUpdateService appUpdateService,
        ILogger<AppReleaseNotesDialogService> logger)
    {
        _appUpdateService = appUpdateService;
        _logger = logger;
    }

    public async Task ShowAsync(XamlRoot xamlRoot, CancellationToken cancellationToken = default)
    {
        AppReleaseNotesState notesState = await _appUpdateService.GetReleaseNotesAsync(cancellationToken);
        AppUpdateState updateState = _appUpdateService.CurrentState;

        StackPanel dialogContent = new()
        {
            Spacing = 12,
            MaxWidth = 760
        };

        dialogContent.Children.Add(new TextBlock
        {
            Text = notesState.StatusLine,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        dialogContent.Children.Add(new TextBlock
        {
            Text = $"Loaded: {notesState.LoadedAtLabel}",
            Style = (Style)Application.Current.Resources["CaptionTextStyle"],
            TextWrapping = TextWrapping.WrapWholeWords
        });

        if (notesState.HasSourceUrl)
        {
            dialogContent.Children.Add(new TextBlock
            {
                Text = notesState.SourceUrl,
                Style = (Style)Application.Current.Resources["CaptionTextStyle"],
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        dialogContent.Children.Add(new TextBox
        {
            Text = notesState.Content,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MinHeight = 380,
            MaxHeight = 520,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });

        ContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = notesState.Title,
            Content = dialogContent,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };

        if (updateState.CanOpenPreferredUpdateUrl)
        {
            dialog.PrimaryButtonText = updateState.PrimaryActionLabel;
            dialog.DefaultButton = ContentDialogButton.Primary;
        }

        if (notesState.HasSourceUrl)
        {
            dialog.SecondaryButtonText = "Open source";
        }

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && updateState.CanOpenPreferredUpdateUrl)
        {
            LaunchExternal(updateState.PreferredUpdateUrl);
            return;
        }

        if (result == ContentDialogResult.Secondary && notesState.HasSourceUrl)
        {
            LaunchExternal(notesState.SourceUrl);
        }
    }

    private void LaunchExternal(string fileName)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Release notes external launch failed for {FileName}.", fileName);
        }
    }
}
