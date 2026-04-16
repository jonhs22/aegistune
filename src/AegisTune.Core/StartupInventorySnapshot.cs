namespace AegisTune.Core;

public sealed record StartupInventorySnapshot(
    IReadOnlyList<StartupEntryRecord> Entries,
    DateTimeOffset ScannedAt,
    string? WarningMessage = null)
{
    public int EntryCount => Entries.Count;

    public int OrphanedCount => Entries.Count(entry => entry.IsOrphaned);

    public int HighImpactCount => Entries.Count(entry => entry.ImpactLevel == StartupImpactLevel.High);

    public int ActionableCount => Entries.Count(entry =>
        entry.IsOrphaned || entry.ImpactLevel == StartupImpactLevel.High);
}
