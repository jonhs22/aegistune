using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioControlService : IAudioControlService
{
    private readonly IAudioPlatformAdapter _platformAdapter;

    public WindowsAudioControlService(IAudioPlatformAdapter platformAdapter)
    {
        _platformAdapter = platformAdapter;
    }

    public Task<AudioControlExecutionResult> AdjustVolumeAsync(
        AudioEndpointRecord endpoint,
        int deltaPercent,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        DateTimeOffset processedAt = DateTimeOffset.Now;
        int targetPercent = Math.Clamp(endpoint.VolumePercent + deltaPercent, 0, 100);
        string actionLabel = deltaPercent >= 0 ? "Increase volume" : "Decrease volume";

        if (dryRunEnabled)
        {
            return Task.FromResult(new AudioControlExecutionResult(
                endpoint.FriendlyName,
                actionLabel,
                true,
                true,
                targetPercent,
                endpoint.IsMuted,
                processedAt,
                $"Previewed {actionLabel.ToLowerInvariant()} for {endpoint.FriendlyName} to {targetPercent:N0}%.",
                "Disable dry-run in Settings when you are ready to change the Windows audio endpoint volume."));
        }

        try
        {
            AudioEndpointRecord updatedEndpoint = _platformAdapter.AdjustVolume(endpoint.DeviceId, deltaPercent);
            return Task.FromResult(BuildSuccessResult(updatedEndpoint, actionLabel, processedAt));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BuildFailureResult(endpoint, actionLabel, processedAt, ex.Message));
        }
    }

    public Task<AudioControlExecutionResult> SetVolumeAsync(
        AudioEndpointRecord endpoint,
        int targetPercent,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        DateTimeOffset processedAt = DateTimeOffset.Now;
        int clampedTarget = Math.Clamp(targetPercent, 0, 100);
        const string actionLabel = "Set volume";

        if (dryRunEnabled)
        {
            return Task.FromResult(new AudioControlExecutionResult(
                endpoint.FriendlyName,
                actionLabel,
                true,
                true,
                clampedTarget,
                endpoint.IsMuted,
                processedAt,
                $"Previewed volume set for {endpoint.FriendlyName} to {clampedTarget:N0}%.",
                "Disable dry-run in Settings when you are ready to apply the Windows audio endpoint volume change."));
        }

        try
        {
            AudioEndpointRecord updatedEndpoint = _platformAdapter.SetVolume(endpoint.DeviceId, clampedTarget);
            return Task.FromResult(BuildSuccessResult(updatedEndpoint, actionLabel, processedAt));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BuildFailureResult(endpoint, actionLabel, processedAt, ex.Message));
        }
    }

    public Task<AudioControlExecutionResult> SetMuteAsync(
        AudioEndpointRecord endpoint,
        bool isMuted,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        DateTimeOffset processedAt = DateTimeOffset.Now;
        string actionLabel = isMuted ? "Mute endpoint" : "Unmute endpoint";

        if (dryRunEnabled)
        {
            return Task.FromResult(new AudioControlExecutionResult(
                endpoint.FriendlyName,
                actionLabel,
                true,
                true,
                endpoint.VolumePercent,
                isMuted,
                processedAt,
                $"Previewed {(isMuted ? "mute" : "unmute")} for {endpoint.FriendlyName}.",
                "Disable dry-run in Settings when you are ready to change the Windows audio endpoint mute state."));
        }

        try
        {
            AudioEndpointRecord updatedEndpoint = _platformAdapter.SetMute(endpoint.DeviceId, isMuted);
            return Task.FromResult(BuildSuccessResult(updatedEndpoint, actionLabel, processedAt));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BuildFailureResult(endpoint, actionLabel, processedAt, ex.Message));
        }
    }

    private static AudioControlExecutionResult BuildSuccessResult(
        AudioEndpointRecord endpoint,
        string actionLabel,
        DateTimeOffset processedAt) =>
        new(
            endpoint.FriendlyName,
            actionLabel,
            false,
            true,
            endpoint.VolumePercent,
            endpoint.IsMuted,
            processedAt,
            $"{actionLabel} completed for {endpoint.FriendlyName}. The endpoint is now {endpoint.MuteLabel.ToLowerInvariant()} at {endpoint.VolumeLabel}.",
            "Review the updated endpoint posture in Audio & Sound or open the Windows sound surfaces for broader routing changes.");

    private static AudioControlExecutionResult BuildFailureResult(
        AudioEndpointRecord endpoint,
        string actionLabel,
        DateTimeOffset processedAt,
        string reason) =>
        new(
            endpoint.FriendlyName,
            actionLabel,
            false,
            false,
            endpoint.VolumePercent,
            endpoint.IsMuted,
            processedAt,
            $"{actionLabel} could not be completed for {endpoint.FriendlyName}: {reason}",
            "Open Windows Sound settings or the classic sound panel when direct endpoint control is unavailable.");
}
