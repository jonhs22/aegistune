using System.Text.Json;
using AegisTune.Core;

namespace AegisTune.Storage;

public sealed class JsonReportStore : IReportStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonReportStore(string? storagePath = null)
    {
        StoragePath = storagePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "reports.json");
    }

    public string StoragePath { get; }

    public async Task<IReadOnlyList<MaintenanceReportRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadInternalAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(MaintenanceReportRecord report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            string directory = Path.GetDirectoryName(StoragePath)
                ?? throw new InvalidOperationException("Unable to determine the report directory.");

            Directory.CreateDirectory(directory);

            List<MaintenanceReportRecord> reports = (await LoadInternalAsync(cancellationToken)).ToList();
            int existingIndex = reports.FindIndex(existing => existing.SummaryFingerprint == report.SummaryFingerprint);
            if (existingIndex >= 0)
            {
                reports[existingIndex] = report;
            }
            else
            {
                reports.Insert(0, report);
            }

            reports = reports
                .OrderByDescending(existing => existing.GeneratedAt)
                .Take(20)
                .ToList();

            await using FileStream stream = File.Create(StoragePath);
            await JsonSerializer.SerializeAsync(stream, reports, SerializerOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<MaintenanceReportRecord>> LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath))
        {
            return Array.Empty<MaintenanceReportRecord>();
        }

        await using FileStream stream = File.OpenRead(StoragePath);
        List<MaintenanceReportRecord>? reports =
            await JsonSerializer.DeserializeAsync<List<MaintenanceReportRecord>>(stream, SerializerOptions, cancellationToken);

        return reports ?? [];
    }
}
