namespace AegisTune.CleanupEngine;

public static class CleanupTargetsCatalog
{
    public static IReadOnlyList<CleanupTargetBlueprint> All { get; } =
        new[]
        {
            new CleanupTargetBlueprint("User temp", "Clears per-user temporary folders and stale application caches.", true),
            new CleanupTargetBlueprint("System temp", "Targets non-critical temporary files created by installers and system tasks.", true),
            new CleanupTargetBlueprint("Recycle Bin", "Measures reclaimable size and supports guided empty for the Windows Recycle Bin.", true),
            new CleanupTargetBlueprint("Scoped browser traces", "Keeps browser cleanup opt-in and tied to an explicit setting.", false)
        };
}
