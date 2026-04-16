using System.Text.Json;
using AegisTune.Core;

namespace AegisTune.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore(string? storagePath = null)
    {
        StoragePath = storagePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "settings.json");
    }

    public string StoragePath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(StoragePath))
            {
                return new AppSettings();
            }

            await using FileStream stream = File.OpenRead(StoragePath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                ?? new AppSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string directory = Path.GetDirectoryName(StoragePath)
                ?? throw new InvalidOperationException("Unable to determine the settings directory.");

            Directory.CreateDirectory(directory);

            await using FileStream stream = File.Create(StoragePath);
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
