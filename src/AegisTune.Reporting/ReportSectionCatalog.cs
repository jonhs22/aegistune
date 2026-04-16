namespace AegisTune.Reporting;

public static class ReportSectionCatalog
{
    public static IReadOnlyList<ReportSectionBlueprint> All { get; } =
        new[]
        {
            new ReportSectionBlueprint("Scan summary", "What the app inspected and how findings were classified."),
            new ReportSectionBlueprint("Proposed actions", "Exactly what will change, why it matters, and whether admin rights are needed."),
            new ReportSectionBlueprint("Execution evidence", "Before-and-after state with timestamps and rollback notes.")
        };
}
