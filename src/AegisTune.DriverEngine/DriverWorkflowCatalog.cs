namespace AegisTune.DriverEngine;

public static class DriverWorkflowCatalog
{
    public static IReadOnlyList<DriverWorkflowStep> All { get; } =
        new[]
        {
            new DriverWorkflowStep("Inventory devices", "Collect device class, provider, version, signer, INF, and hardware IDs before you touch any package source."),
            new DriverWorkflowStep("Score review candidates", "Separate bad-status, unsigned, and generic-provider cases from healthy devices before deciding whether action is needed."),
            new DriverWorkflowStep("Choose the source path", "Use Windows Update or the OEM package only when the hardware evidence matches the exact device and model."),
            new DriverWorkflowStep("Verify after change", "Re-check the device state, driver version, and rollback evidence after any install or reboot.")
        };
}
