using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class WindowsAudioInventoryServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_UsesSettingsFloorAndSortsEndpoints()
    {
        FakeAudioPlatformAdapter adapter = new(
            [
                new AudioEndpointRecord("recording-default", "Microphone", AudioEndpointKind.Recording, true, false, 55, false, "Active"),
                new AudioEndpointRecord("playback-secondary", "Monitor", AudioEndpointKind.Playback, false, false, 80, false, "Active"),
                new AudioEndpointRecord("playback-default", "Speakers", AudioEndpointKind.Playback, true, false, 45, false, "Active")
            ]);
        WindowsAudioInventoryService service = new(
            adapter,
            new FakeSettingsStore(new AppSettings(AudioRecommendedVolumePercent: 70)));

        AudioInventorySnapshot snapshot = await service.GetSnapshotAsync();

        Assert.Equal(70, snapshot.RecommendedVolumePercent);
        Assert.Equal("Speakers", snapshot.PlaybackDevices[0].FriendlyName);
        Assert.Equal("Microphone", snapshot.RecordingDevices[0].FriendlyName);
        Assert.Equal(2, snapshot.IssueCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenAdapterFails_ReturnsWarningSnapshot()
    {
        WindowsAudioInventoryService service = new(
            new ThrowingAudioPlatformAdapter(),
            new FakeSettingsStore(new AppSettings()));

        AudioInventorySnapshot snapshot = await service.GetSnapshotAsync();

        Assert.Empty(snapshot.PlaybackDevices);
        Assert.Empty(snapshot.RecordingDevices);
        Assert.Contains("Audio inventory failed", snapshot.WarningMessage, StringComparison.Ordinal);
    }

    private sealed class FakeAudioPlatformAdapter : IAudioPlatformAdapter
    {
        private readonly IReadOnlyList<AudioEndpointRecord> _endpoints;

        public FakeAudioPlatformAdapter(IReadOnlyList<AudioEndpointRecord> endpoints)
        {
            _endpoints = endpoints;
        }

        public IReadOnlyList<AudioEndpointRecord> EnumerateActiveEndpoints() => _endpoints;

        public AudioEndpointRecord SetVolume(string deviceId, int targetPercent) => throw new NotSupportedException();

        public AudioEndpointRecord AdjustVolume(string deviceId, int deltaPercent) => throw new NotSupportedException();

        public AudioEndpointRecord SetMute(string deviceId, bool isMuted) => throw new NotSupportedException();
    }

    private sealed class ThrowingAudioPlatformAdapter : IAudioPlatformAdapter
    {
        public IReadOnlyList<AudioEndpointRecord> EnumerateActiveEndpoints() =>
            throw new InvalidOperationException("Core Audio unavailable");

        public AudioEndpointRecord SetVolume(string deviceId, int targetPercent) => throw new NotSupportedException();

        public AudioEndpointRecord AdjustVolume(string deviceId, int deltaPercent) => throw new NotSupportedException();

        public AudioEndpointRecord SetMute(string deviceId, bool isMuted) => throw new NotSupportedException();
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly AppSettings _settings;

        public FakeSettingsStore(AppSettings settings)
        {
            _settings = settings;
        }

        public string StoragePath => "memory";

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
