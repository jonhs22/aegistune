namespace AegisTune.RepairEngine;

public static class RepairPlaybookCatalog
{
    public static IReadOnlyList<RepairPlaybookItem> All { get; } =
        new[]
        {
            new RepairPlaybookItem("Orphaned startup entry", "Only when the target path is missing and the source entry is still present."),
            new RepairPlaybookItem("Official runtime repair", "Only when Windows evidence shows a missing or broken VC++/UCRT/WebView2/DirectX dependency. Repair from Microsoft or the official vendor only."),
            new RepairPlaybookItem("Vendor DLL restoration", "Only when the missing DLL is app-local and the app can be tied to a vendor repair or reinstall path. Never use third-party DLL mirrors."),
            new RepairPlaybookItem("Scheduled task leftover", "Only when the task points to a missing executable or removed product."),
            new RepairPlaybookItem("Broken shell remnant", "Only when the handler reference is invalid and rollback is available.")
        };
}
