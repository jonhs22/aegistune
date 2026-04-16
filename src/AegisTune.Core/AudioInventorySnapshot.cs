namespace AegisTune.Core;

public sealed record AudioInventorySnapshot(
    IReadOnlyList<AudioEndpointRecord> PlaybackDevices,
    IReadOnlyList<AudioEndpointRecord> RecordingDevices,
    int RecommendedVolumePercent,
    DateTimeOffset CollectedAt,
    string? WarningMessage = null)
{
    public AudioEndpointRecord? DefaultPlaybackDevice =>
        PlaybackDevices.FirstOrDefault(device => device.IsDefault)
        ?? PlaybackDevices.FirstOrDefault(device => device.IsDefaultCommunication);

    public AudioEndpointRecord? DefaultRecordingDevice =>
        RecordingDevices.FirstOrDefault(device => device.IsDefault)
        ?? RecordingDevices.FirstOrDefault(device => device.IsDefaultCommunication);

    public int OutputDeviceCount => PlaybackDevices.Count;

    public int InputDeviceCount => RecordingDevices.Count;

    public int MutedEndpointCount => PlaybackDevices.Concat(RecordingDevices).Count(device => device.IsMuted);

    public int LowVolumeEndpointCount => PlaybackDevices
        .Concat(RecordingDevices)
        .Count(device => !device.IsMuted && device.VolumePercent < RecommendedVolumePercent);

    public int IssueCount
    {
        get
        {
            int issueCount = 0;

            if (DefaultPlaybackDevice is null)
            {
                issueCount++;
            }
            else if (DefaultPlaybackDevice.IsMuted || DefaultPlaybackDevice.VolumePercent < RecommendedVolumePercent)
            {
                issueCount++;
            }

            if (DefaultRecordingDevice is null)
            {
                issueCount++;
            }
            else if (DefaultRecordingDevice.IsMuted || DefaultRecordingDevice.VolumePercent < RecommendedVolumePercent)
            {
                issueCount++;
            }

            return issueCount;
        }
    }

    public string StatusLine
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WarningMessage))
            {
                return WarningMessage!;
            }

            if (OutputDeviceCount == 0 && InputDeviceCount == 0)
            {
                return "No active playback or recording devices are currently exposed by Windows.";
            }

            if (IssueCount == 0)
            {
                return $"Default playback and recording devices are available. {MutedEndpointCount:N0} muted endpoint(s) and {LowVolumeEndpointCount:N0} low-volume endpoint(s) remain available for optional review.";
            }

            return $"{IssueCount:N0} default audio control item(s) need review. {MutedEndpointCount:N0} endpoint(s) are muted and {LowVolumeEndpointCount:N0} endpoint(s) are below the recommended {RecommendedVolumePercent:N0}% target.";
        }
    }
}
