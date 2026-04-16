using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class WindowsAudioControlServiceTests
{
    [Fact]
    public async Task AdjustVolumeAsync_InDryRun_DoesNotTouchAdapter()
    {
        RecordingAudioPlatformAdapter adapter = new(
            new AudioEndpointRecord("playback", "Speakers", AudioEndpointKind.Playback, true, false, 45, false, "Active"));
        WindowsAudioControlService service = new(adapter);

        AudioControlExecutionResult result = await service.AdjustVolumeAsync(
            adapter.Endpoint,
            10,
            dryRunEnabled: true);

        Assert.True(result.WasDryRun);
        Assert.True(result.Succeeded);
        Assert.Equal(55, result.VolumePercent);
        Assert.Equal(0, adapter.CallCount);
    }

    [Fact]
    public async Task SetMuteAsync_InLiveMode_UsesAdapterResult()
    {
        RecordingAudioPlatformAdapter adapter = new(
            new AudioEndpointRecord("playback", "Speakers", AudioEndpointKind.Playback, true, false, 45, false, "Active"));
        WindowsAudioControlService service = new(adapter);

        AudioControlExecutionResult result = await service.SetMuteAsync(
            adapter.Endpoint,
            isMuted: true,
            dryRunEnabled: false);

        Assert.False(result.WasDryRun);
        Assert.True(result.Succeeded);
        Assert.True(result.IsMuted);
        Assert.Equal(1, adapter.CallCount);
        Assert.Equal("mute", adapter.LastAction);
    }

    [Fact]
    public async Task SetVolumeAsync_WhenAdapterFails_ReturnsFailureResult()
    {
        WindowsAudioControlService service = new(new FailingAudioPlatformAdapter());

        AudioControlExecutionResult result = await service.SetVolumeAsync(
            new AudioEndpointRecord("playback", "Speakers", AudioEndpointKind.Playback, true, false, 45, false, "Active"),
            60,
            dryRunEnabled: false);

        Assert.False(result.Succeeded);
        Assert.Contains("could not be completed", result.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingAudioPlatformAdapter : IAudioPlatformAdapter
    {
        public RecordingAudioPlatformAdapter(AudioEndpointRecord endpoint)
        {
            Endpoint = endpoint;
        }

        public AudioEndpointRecord Endpoint { get; private set; }

        public int CallCount { get; private set; }

        public string? LastAction { get; private set; }

        public IReadOnlyList<AudioEndpointRecord> EnumerateActiveEndpoints() => [Endpoint];

        public AudioEndpointRecord SetVolume(string deviceId, int targetPercent)
        {
            CallCount++;
            LastAction = "set-volume";
            Endpoint = Endpoint with { VolumePercent = Math.Clamp(targetPercent, 0, 100) };
            return Endpoint;
        }

        public AudioEndpointRecord AdjustVolume(string deviceId, int deltaPercent)
        {
            CallCount++;
            LastAction = "adjust-volume";
            Endpoint = Endpoint with { VolumePercent = Math.Clamp(Endpoint.VolumePercent + deltaPercent, 0, 100) };
            return Endpoint;
        }

        public AudioEndpointRecord SetMute(string deviceId, bool isMuted)
        {
            CallCount++;
            LastAction = "mute";
            Endpoint = Endpoint with { IsMuted = isMuted };
            return Endpoint;
        }
    }

    private sealed class FailingAudioPlatformAdapter : IAudioPlatformAdapter
    {
        public IReadOnlyList<AudioEndpointRecord> EnumerateActiveEndpoints() => Array.Empty<AudioEndpointRecord>();

        public AudioEndpointRecord SetVolume(string deviceId, int targetPercent) =>
            throw new InvalidOperationException("Audio endpoint is unavailable");

        public AudioEndpointRecord AdjustVolume(string deviceId, int deltaPercent) =>
            throw new InvalidOperationException("Audio endpoint is unavailable");

        public AudioEndpointRecord SetMute(string deviceId, bool isMuted) =>
            throw new InvalidOperationException("Audio endpoint is unavailable");
    }
}
