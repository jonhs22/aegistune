using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioInventoryService : IAudioInventoryService
{
    private readonly IAudioPlatformAdapter _platformAdapter;
    private readonly ISettingsStore _settingsStore;

    public WindowsAudioInventoryService(IAudioPlatformAdapter platformAdapter, ISettingsStore settingsStore)
    {
        _platformAdapter = platformAdapter;
        _settingsStore = settingsStore;
    }

    public async Task<AudioInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);

        return await Task.Run(
            () =>
            {
                DateTimeOffset collectedAt = DateTimeOffset.Now;

                try
                {
                    IReadOnlyList<AudioEndpointRecord> endpoints = _platformAdapter.EnumerateActiveEndpoints();
                    AudioEndpointRecord[] playbackDevices = endpoints
                        .Where(endpoint => endpoint.Kind == AudioEndpointKind.Playback)
                        .OrderByDescending(endpoint => endpoint.IsDefault)
                        .ThenByDescending(endpoint => endpoint.IsDefaultCommunication)
                        .ThenBy(endpoint => endpoint.FriendlyName, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    AudioEndpointRecord[] recordingDevices = endpoints
                        .Where(endpoint => endpoint.Kind == AudioEndpointKind.Recording)
                        .OrderByDescending(endpoint => endpoint.IsDefault)
                        .ThenByDescending(endpoint => endpoint.IsDefaultCommunication)
                        .ThenBy(endpoint => endpoint.FriendlyName, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    return new AudioInventorySnapshot(
                        playbackDevices,
                        recordingDevices,
                        settings.EffectiveAudioRecommendedVolumePercent,
                        collectedAt);
                }
                catch (Exception ex)
                {
                    return new AudioInventorySnapshot(
                        Array.Empty<AudioEndpointRecord>(),
                        Array.Empty<AudioEndpointRecord>(),
                        settings.EffectiveAudioRecommendedVolumePercent,
                        collectedAt,
                        $"Audio inventory failed: {ex.Message}");
                }
            },
            cancellationToken);
    }
}
