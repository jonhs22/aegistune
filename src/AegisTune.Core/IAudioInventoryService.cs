namespace AegisTune.Core;

public interface IAudioInventoryService
{
    Task<AudioInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
