using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AegisTune.App.Pages;

public sealed partial class AudioPage : Page
{
    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private AppSettings _settings = new();

    public AudioPage()
    {
        InitializeComponent();
        Loaded += AudioPage_Loaded;
    }

    public ModuleSnapshot? Module { get; private set; }

    public AudioInventorySnapshot? Snapshot { get; private set; }

    public IReadOnlyList<AudioEndpointRecord> PlaybackDevices => Snapshot?.PlaybackDevices ?? Array.Empty<AudioEndpointRecord>();

    public IReadOnlyList<AudioEndpointRecord> RecordingDevices => Snapshot?.RecordingDevices ?? Array.Empty<AudioEndpointRecord>();

    public AudioEndpointRecord? DefaultPlaybackDevice => Snapshot?.DefaultPlaybackDevice;

    public AudioEndpointRecord? DefaultRecordingDevice => Snapshot?.DefaultRecordingDevice;

    public bool HasDefaultPlaybackDevice => DefaultPlaybackDevice is not null;

    public bool HasDefaultRecordingDevice => DefaultRecordingDevice is not null;

    public string ModuleSubtitle => Module?.Subtitle ?? "Review playback and recording device posture.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading audio posture.";

    public string ActionStatusLine => _actionStatusMessage
        ?? "Choose the default playback or recording device first, then adjust volume or mute only for that endpoint.";

    public string HeroSummaryLine => Snapshot is null
        ? "Scanning Windows playback and recording devices."
        : Snapshot.IssueCount == 0
            ? "Default playback and recording devices are available for the current Windows session."
            : $"{Snapshot.IssueCount:N0} default audio item(s) need review across playback and recording.";

    public string AudioDefaultsSummaryLine =>
        $"Audio step is {_settings.EffectiveAudioVolumeStepPercent:N0}% and the review floor is {_settings.EffectiveAudioRecommendedVolumePercent:N0}% from Settings.";

    public string EndpointCountLabel => Snapshot is null
        ? "--"
        : (Snapshot.OutputDeviceCount + Snapshot.InputDeviceCount).ToString("N0");

    public string OutputInputSplitLabel => Snapshot is null
        ? "Playback -- • Recording --"
        : $"Playback {Snapshot.OutputDeviceCount:N0} • Recording {Snapshot.InputDeviceCount:N0}";

    public string IssueCountLabel => Snapshot?.IssueCount.ToString("N0") ?? "--";

    public string RecommendedVolumeLabel => $"Review floor {_settings.EffectiveAudioRecommendedVolumePercent:N0}%";

    public string MutedCountLabel => Snapshot?.MutedEndpointCount.ToString("N0") ?? "--";

    public string LowVolumeCountLabel => Snapshot is null
        ? "Low-volume endpoints --"
        : $"Low-volume endpoints {Snapshot.LowVolumeEndpointCount:N0}";

    public string DefaultRouteSummaryLabel
    {
        get
        {
            if (Snapshot is null)
            {
                return "--";
            }

            return $"{(DefaultPlaybackDevice is null ? "No output" : "Output ready")} • {(DefaultRecordingDevice is null ? "No input" : "Input ready")}";
        }
    }

    public string SnapshotCollectedAtLabel => Snapshot is null
        ? "Snapshot pending."
        : $"Collected {Snapshot.CollectedAt.ToLocalTime():g}";

    public string DefaultPlaybackName => DefaultPlaybackDevice?.FriendlyName ?? "No default playback device";

    public string DefaultPlaybackSummary => DefaultPlaybackDevice is null
        ? "Windows did not expose a default playback device in the current session."
        : $"{DefaultPlaybackDevice.StatusSummary} • {DefaultPlaybackDevice.StateLabel}";

    public string DefaultPlaybackMuteActionLabel => DefaultPlaybackDevice?.ToggleMuteLabel ?? "Mute";

    public string DefaultRecordingName => DefaultRecordingDevice?.FriendlyName ?? "No default recording device";

    public string DefaultRecordingSummary => DefaultRecordingDevice is null
        ? "Windows did not expose a default recording device in the current session."
        : $"{DefaultRecordingDevice.StatusSummary} • {DefaultRecordingDevice.StateLabel}";

    public string DefaultRecordingMuteActionLabel => DefaultRecordingDevice?.ToggleMuteLabel ?? "Mute";

    public string PlaybackSummary => PlaybackDevices.Count == 0
        ? "No active playback devices are currently exposed by Windows."
        : "Review speaker, headset, HDMI, or monitor outputs here. Adjust only the endpoint you actually want for this session.";

    public string RecordingSummary => RecordingDevices.Count == 0
        ? "No active recording devices are currently exposed by Windows."
        : "Review microphones and capture endpoints here. Keep routing changes on the native Windows recording surfaces.";

    public string WindowsToolsSummary =>
        "Use the native Windows sound tools when you need endpoint switching, per-app volume, device tests, or privacy routing. AegisTune keeps direct control limited to endpoint volume and mute.";

    private async void AudioPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (Module is not null && Snapshot is not null)
        {
            return;
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            Task<DashboardSnapshot> dashboardTask = App.GetService<IDashboardSnapshotService>().GetSnapshotAsync();
            Task<AudioInventorySnapshot> audioTask = App.GetService<IAudioInventoryService>().GetSnapshotAsync();
            Task<AppSettings> settingsTask = App.GetService<ISettingsStore>().LoadAsync();

            DashboardSnapshot dashboard = await dashboardTask;
            Snapshot = await audioTask;
            _settings = await settingsTask;
            Module = dashboard.GetModule(AppSection.Audio);
            _loadErrorMessage = null;
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The audio review could not be completed.";
            App.GetService<ILogger<AudioPage>>().LogError(ex, "Audio page failed to load.");
        }

        Bindings.Update();
    }

    private async void RefreshAudio_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing audio posture.";
        Bindings.Update();
        await ReloadAsync();
        _actionStatusMessage = "Audio posture refreshed.";
        Bindings.Update();
    }

    private async void IncreaseEndpointVolume_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEndpoint(sender, out AudioEndpointRecord? endpoint))
        {
            return;
        }

        await ExecuteAudioChangeAsync(
            () => App.GetService<IAudioControlService>().AdjustVolumeAsync(
                endpoint,
                _settings.EffectiveAudioVolumeStepPercent,
                _settings.DryRunEnabled));
    }

    private async void DecreaseEndpointVolume_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEndpoint(sender, out AudioEndpointRecord? endpoint))
        {
            return;
        }

        await ExecuteAudioChangeAsync(
            () => App.GetService<IAudioControlService>().AdjustVolumeAsync(
                endpoint,
                -_settings.EffectiveAudioVolumeStepPercent,
                _settings.DryRunEnabled));
    }

    private async void ToggleEndpointMute_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEndpoint(sender, out AudioEndpointRecord? endpoint))
        {
            return;
        }

        await ExecuteAudioChangeAsync(
            () => App.GetService<IAudioControlService>().SetMuteAsync(
                endpoint,
                !endpoint.IsMuted,
                _settings.DryRunEnabled));
    }

    private async void SetEndpointRecommendedVolume_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedEndpoint(sender, out AudioEndpointRecord? endpoint))
        {
            return;
        }

        await ExecuteAudioChangeAsync(
            () => App.GetService<IAudioControlService>().SetVolumeAsync(
                endpoint,
                _settings.EffectiveAudioRecommendedVolumePercent,
                _settings.DryRunEnabled));
    }

    private void OpenSoundSettings_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("ms-settings:sound", null, "Opened Sound settings.");

    private void OpenVolumeMixer_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("ms-settings:apps-volume", null, "Opened the Windows volume mixer.");

    private void OpenClassicPlaybackPanel_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("control.exe", "mmsys.cpl,,0", "Opened the classic Playback devices panel.");

    private void OpenClassicRecordingPanel_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("control.exe", "mmsys.cpl,,1", "Opened the classic Recording devices panel.");

    private void OpenSoundControlPanel_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("mmsys.cpl", null, "Opened the classic sound panel.");

    private void OpenMicrophonePrivacy_Click(object sender, RoutedEventArgs e) =>
        LaunchExternal("ms-settings:privacy-microphone", null, "Opened microphone privacy settings.");

    private async Task ExecuteAudioChangeAsync(Func<Task<AudioControlExecutionResult>> action)
    {
        try
        {
            AudioControlExecutionResult result = await action();
            _actionStatusMessage = result.StatusLine;

            if (result.Succeeded && !result.WasDryRun)
            {
                await ReloadAsync();
                _actionStatusMessage = result.StatusLine;
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The requested audio action could not be completed: {ex.Message}";
            App.GetService<ILogger<AudioPage>>().LogError(ex, "Audio action failed.");
        }

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
            _actionStatusMessage = $"The requested Windows sound surface could not be opened: {ex.Message}";
            App.GetService<ILogger<AudioPage>>().LogError(ex, "Audio page action failed for {FileName}.", fileName);
        }

        Bindings.Update();
    }

    private static bool TryGetTaggedEndpoint(object sender, [NotNullWhen(true)] out AudioEndpointRecord? endpoint)
    {
        endpoint = (sender as FrameworkElement)?.Tag as AudioEndpointRecord;
        return endpoint is not null;
    }
}
