namespace AegisTune.Core;

public interface IAudioPlatformAdapter
{
    IReadOnlyList<AudioEndpointRecord> EnumerateActiveEndpoints();

    AudioEndpointRecord SetVolume(string deviceId, int targetPercent);

    AudioEndpointRecord AdjustVolume(string deviceId, int deltaPercent);

    AudioEndpointRecord SetMute(string deviceId, bool isMuted);
}
