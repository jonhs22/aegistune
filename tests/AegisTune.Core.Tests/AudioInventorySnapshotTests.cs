using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class AudioInventorySnapshotTests
{
    [Fact]
    public void IssueCount_UsesDefaultEndpointsAgainstRecommendedFloor()
    {
        AudioInventorySnapshot snapshot = new(
            [
                new AudioEndpointRecord("playback", "Speakers", AudioEndpointKind.Playback, true, false, 35, false, "Active")
            ],
            [
                new AudioEndpointRecord("recording", "Microphone", AudioEndpointKind.Recording, true, false, 80, true, "Active")
            ],
            60,
            DateTimeOffset.Now);

        Assert.Equal(2, snapshot.IssueCount);
        Assert.Equal(1, snapshot.MutedEndpointCount);
        Assert.Equal(1, snapshot.LowVolumeEndpointCount);
        Assert.Contains("2 default audio control item(s) need review", snapshot.StatusLine);
    }

    [Fact]
    public void DefaultEndpoints_FallBackToCommunicationsRoute()
    {
        AudioEndpointRecord communicationsPlayback = new("playback-comm", "Headset", AudioEndpointKind.Playback, false, true, 65, false, "Active");
        AudioEndpointRecord communicationsRecording = new("recording-comm", "Headset Mic", AudioEndpointKind.Recording, false, true, 65, false, "Active");

        AudioInventorySnapshot snapshot = new(
            [communicationsPlayback],
            [communicationsRecording],
            60,
            DateTimeOffset.Now);

        Assert.Same(communicationsPlayback, snapshot.DefaultPlaybackDevice);
        Assert.Same(communicationsRecording, snapshot.DefaultRecordingDevice);
        Assert.Equal(0, snapshot.IssueCount);
    }
}
