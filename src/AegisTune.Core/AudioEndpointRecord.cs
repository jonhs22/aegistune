namespace AegisTune.Core;

public sealed record AudioEndpointRecord(
    string DeviceId,
    string FriendlyName,
    AudioEndpointKind Kind,
    bool IsDefault,
    bool IsDefaultCommunication,
    int VolumePercent,
    bool IsMuted,
    string StateLabel)
{
    public string KindLabel => Kind == AudioEndpointKind.Playback ? "Playback device" : "Recording device";

    public string DefaultRoleLabel => (IsDefault, IsDefaultCommunication) switch
    {
        (true, true) => "Default device and communications device",
        (true, false) => "Default device",
        (false, true) => "Default communications device",
        _ => "Available device"
    };

    public string VolumeLabel => $"{VolumePercent:N0}%";

    public string MuteLabel => IsMuted ? "Muted" : "Live";

    public string StatusSummary => $"{DefaultRoleLabel} • {MuteLabel} • {VolumeLabel}";

    public string ToggleMuteLabel => IsMuted ? "Unmute" : "Mute";
}
