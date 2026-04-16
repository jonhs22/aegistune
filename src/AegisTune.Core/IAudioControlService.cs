namespace AegisTune.Core;

public interface IAudioControlService
{
    Task<AudioControlExecutionResult> AdjustVolumeAsync(
        AudioEndpointRecord endpoint,
        int deltaPercent,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);

    Task<AudioControlExecutionResult> SetVolumeAsync(
        AudioEndpointRecord endpoint,
        int targetPercent,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);

    Task<AudioControlExecutionResult> SetMuteAsync(
        AudioEndpointRecord endpoint,
        bool isMuted,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default);
}
