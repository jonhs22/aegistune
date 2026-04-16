using System.Text.Json;
using AegisTune.Core;

namespace AegisTune.Storage;

public sealed class JsonUndoJournalStore : IUndoJournalStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonUndoJournalStore(string? storagePath = null)
    {
        StoragePath = storagePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "undo-journal.json");
    }

    public string StoragePath { get; }

    public async Task<IReadOnlyList<UndoJournalEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(StoragePath))
            {
                return Array.Empty<UndoJournalEntry>();
            }

            await using FileStream stream = File.OpenRead(StoragePath);
            return await JsonSerializer.DeserializeAsync<List<UndoJournalEntry>>(stream, SerializerOptions, cancellationToken)
                ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string directory = Path.GetDirectoryName(StoragePath)
                ?? throw new InvalidOperationException("Unable to determine the undo journal directory.");
            Directory.CreateDirectory(directory);

            List<UndoJournalEntry> entries;
            if (File.Exists(StoragePath))
            {
                await using FileStream readStream = File.OpenRead(StoragePath);
                entries = await JsonSerializer.DeserializeAsync<List<UndoJournalEntry>>(readStream, SerializerOptions, cancellationToken)
                    ?? [];
            }
            else
            {
                entries = [];
            }

            entries.Insert(0, entry);
            if (entries.Count > 160)
            {
                entries = entries.Take(160).ToList();
            }

            await using FileStream writeStream = File.Create(StoragePath);
            await JsonSerializer.SerializeAsync(writeStream, entries, SerializerOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
