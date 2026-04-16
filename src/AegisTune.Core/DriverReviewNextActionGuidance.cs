namespace AegisTune.Core;

public enum DriverReviewNextActionKind
{
    None,
    InstallSelectedLocalDriver,
    OpenDeviceManager,
    OpenWindowsUpdate,
    CopyTechnicianBrief,
    OpenSettings,
    OpenSelectedInf
}

public sealed record DriverReviewNextActionGuidance(
    string Headline,
    string Summary,
    string NextStep,
    DriverReviewNextActionKind PrimaryActionKind,
    string PrimaryActionLabel,
    DriverReviewNextActionKind SecondaryActionKind,
    string SecondaryActionLabel)
{
    public bool HasPrimaryAction => PrimaryActionKind != DriverReviewNextActionKind.None && !string.IsNullOrWhiteSpace(PrimaryActionLabel);

    public bool HasSecondaryAction => SecondaryActionKind != DriverReviewNextActionKind.None && !string.IsNullOrWhiteSpace(SecondaryActionLabel);

    public static DriverReviewNextActionGuidance Empty { get; } = new(
        "Choose a device from the review queue",
        "The selected-device panel and action buttons update only after one queue card is selected.",
        "Start by clicking one device in the review queue above.",
        DriverReviewNextActionKind.None,
        string.Empty,
        DriverReviewNextActionKind.None,
        string.Empty);
}

public static class DriverReviewNextActionAdvisor
{
    public static DriverReviewNextActionGuidance Create(
        DriverDeviceRecord? device,
        DriverRepositoryCandidate? selectedCandidate,
        bool hasRepositoryRoots)
    {
        if (device is null)
        {
            return DriverReviewNextActionGuidance.Empty;
        }

        if (selectedCandidate is not null)
        {
            return new DriverReviewNextActionGuidance(
                "A vetted local INF is ready",
                $"{selectedCandidate.SummaryLine} is selected for {device.FriendlyName}.",
                "Use the install lane only if this vetted INF is the intended path for the selected device.",
                DriverReviewNextActionKind.InstallSelectedLocalDriver,
                "Install selected local driver",
                DriverReviewNextActionKind.OpenSelectedInf,
                "Open selected INF");
        }

        if (device.RequiresPriorityReview)
        {
            return new DriverReviewNextActionGuidance(
                "Start with Device Manager",
                $"{device.HealthLabel}. {device.RecommendedAction}",
                "This device is in a risky state. Confirm the live problem and current package details in Device Manager before any package change.",
                DriverReviewNextActionKind.OpenDeviceManager,
                "Open Device Manager",
                DriverReviewNextActionKind.CopyTechnicianBrief,
                "Copy device brief");
        }

        if (!hasRepositoryRoots && device.HasHardwareIds)
        {
            return new DriverReviewNextActionGuidance(
                "Add local repositories first",
                "This device has hardware evidence, but no vetted local repository roots are configured yet.",
                "Open Settings, add one or more trusted driver repository folders, then refresh this module to build INF matches.",
                DriverReviewNextActionKind.OpenSettings,
                "Open Settings",
                DriverReviewNextActionKind.CopyTechnicianBrief,
                "Copy device brief");
        }

        if (device.EvidenceTier == DriverEvidenceTier.NoIdentifierEvidence)
        {
            return new DriverReviewNextActionGuidance(
                "Keep this on technician review",
                device.EvidenceTierDescription,
                "The current evidence is too weak for automation. Copy the brief and keep the fix path manual.",
                DriverReviewNextActionKind.CopyTechnicianBrief,
                "Copy device brief",
                DriverReviewNextActionKind.OpenDeviceManager,
                "Open Device Manager");
        }

        if (device.EvidenceTier == DriverEvidenceTier.CompatibleFallback || device.UsesGenericProviderReview)
        {
            return new DriverReviewNextActionGuidance(
                "Use a safe Windows review lane",
                device.RecommendedAction,
                "Prefer Windows Update or technician review before any package swap when the evidence is only compatible-ID fallback or the provider still looks generic.",
                DriverReviewNextActionKind.OpenWindowsUpdate,
                "Open Windows Update",
                DriverReviewNextActionKind.CopyTechnicianBrief,
                "Copy device brief");
        }

        if (!string.IsNullOrWhiteSpace(device.InfName))
        {
            return new DriverReviewNextActionGuidance(
                "Review the current driver package",
                device.SafeSourcePath,
                "Open the current INF first if you want to inspect the package already active on this device.",
                DriverReviewNextActionKind.OpenSelectedInf,
                "Open current INF",
                DriverReviewNextActionKind.CopyTechnicianBrief,
                "Copy device brief");
        }

        return new DriverReviewNextActionGuidance(
            "Review the device brief",
            device.RecommendedAction,
            "There is no automatic local INF action for this device right now, so keep the next step explicit and technician-led.",
            DriverReviewNextActionKind.CopyTechnicianBrief,
            "Copy device brief",
            DriverReviewNextActionKind.OpenDeviceManager,
            "Open Device Manager");
    }
}
