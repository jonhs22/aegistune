namespace AegisTune.Core;

public interface ISettingsStore
{
    string StoragePath { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
