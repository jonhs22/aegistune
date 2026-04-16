using AegisTune.Core;

namespace AegisTune.DriverEngine;

public static class DriverRemediationPlanner
{
    private static readonly HashSet<string> LikelyRebootClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display",
        "HDC",
        "Net",
        "SCSIAdapter",
        "System"
    };

    private static readonly HashSet<string> MaybeRebootClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bluetooth",
        "Camera",
        "MEDIA",
        "USB"
    };

    public static DriverRemediationPlan Build(DriverDeviceRecord device)
    {
        ArgumentNullException.ThrowIfNull(device);

        DriverRemediationSource source = ResolveSource(device);
        IReadOnlyList<DriverVerificationStep> verificationSteps = BuildVerificationSteps(device, source);

        return new DriverRemediationPlan(
            source,
            ResolveRebootGuidance(device, source),
            BuildSummary(device, source),
            BuildSourceLabel(source),
            BuildSourceReason(device, source),
            BuildRollbackLabel(source),
            BuildRollbackDetail(device, source),
            verificationSteps);
    }

    private static DriverRemediationSource ResolveSource(DriverDeviceRecord device)
    {
        if (device.ReviewRiskLevel == RiskLevel.Safe)
        {
            return DriverRemediationSource.MonitorOnly;
        }

        if (device.EvidenceTier != DriverEvidenceTier.HardwareBacked)
        {
            return DriverRemediationSource.ManualTechnicianHandoff;
        }

        if (device.RequiresPriorityReview && device.MatchConfidence == DriverMatchConfidence.High)
        {
            return DriverRemediationSource.ExactOemPackage;
        }

        if (device.UsesGenericProviderReview && device.HasHardwareIds)
        {
            return DriverRemediationSource.WindowsUpdateComparison;
        }

        if (!string.IsNullOrWhiteSpace(device.InfName) && device.MatchConfidence is DriverMatchConfidence.High or DriverMatchConfidence.Medium)
        {
            return DriverRemediationSource.LocalInfReview;
        }

        if (device.MatchConfidence is DriverMatchConfidence.High or DriverMatchConfidence.Medium)
        {
            return DriverRemediationSource.ExactOemPackage;
        }

        return DriverRemediationSource.ManualTechnicianHandoff;
    }

    private static DriverRebootGuidance ResolveRebootGuidance(DriverDeviceRecord device, DriverRemediationSource source)
    {
        if (source == DriverRemediationSource.MonitorOnly)
        {
            return DriverRebootGuidance.NotExpected;
        }

        if (LikelyRebootClasses.Contains(device.DeviceClass))
        {
            return DriverRebootGuidance.LikelyRequired;
        }

        if (MaybeRebootClasses.Contains(device.DeviceClass) || device.IsCriticalClass)
        {
            return DriverRebootGuidance.MayBeRequired;
        }

        return DriverRebootGuidance.NotExpected;
    }

    private static string BuildSummary(DriverDeviceRecord device, DriverRemediationSource source) => source switch
    {
        DriverRemediationSource.MonitorOnly => "No active remediation path is recommended. Keep the device under audit and re-check after Windows or OEM updates.",
        DriverRemediationSource.WindowsUpdateComparison => $"Use Windows Update as the first comparison path for {device.FriendlyName}, then verify whether the current Microsoft-supplied package should stay in place or be replaced by an exact OEM release.",
        DriverRemediationSource.ExactOemPackage => $"Prepare an exact OEM remediation path for {device.FriendlyName} because the current hardware-backed evidence is strong enough to compare against a specific package.",
        DriverRemediationSource.LocalInfReview => $"Review the currently installed INF and compare it against the exact OEM release notes before you change {device.FriendlyName}.",
        _ => $"Keep {device.FriendlyName} on a technician handoff path until the provider, hardware evidence, and model context align more cleanly."
    };

    private static string BuildSourceLabel(DriverRemediationSource source) => source switch
    {
        DriverRemediationSource.MonitorOnly => "Monitor only",
        DriverRemediationSource.WindowsUpdateComparison => "Windows Update comparison",
        DriverRemediationSource.ExactOemPackage => "Exact OEM package",
        DriverRemediationSource.LocalInfReview => "Local INF review",
        _ => "Manual technician handoff"
    };

    private static string BuildSourceReason(DriverDeviceRecord device, DriverRemediationSource source) => source switch
    {
        DriverRemediationSource.MonitorOnly => "The device is currently healthy enough that the safest path is to keep the audit trail and avoid unnecessary driver churn.",
        DriverRemediationSource.WindowsUpdateComparison => $"The device is on a generic or Microsoft-supplied path, but the current {device.MatchEvidenceSourceLabel.ToLowerInvariant()} evidence is still good enough to compare Windows Update against the OEM baseline.",
        DriverRemediationSource.ExactOemPackage => $"The device has hardware-backed evidence with {device.MatchConfidenceLabel.ToLowerInvariant()} and should be compared only against a package that matches the current model identifiers.",
        DriverRemediationSource.LocalInfReview => $"The installed INF is available locally and the evidence is good enough to inspect the current package details before deciding whether an OEM swap is justified.",
        _ => $"The current evidence tier is {device.EvidenceTierLabel.ToLowerInvariant()} with {device.MatchConfidenceLabel.ToLowerInvariant()}, so the package source should stay on a manual review path."
    };

    private static string BuildRollbackLabel(DriverRemediationSource source) => source switch
    {
        DriverRemediationSource.MonitorOnly => "No rollback staging",
        DriverRemediationSource.LocalInfReview => "Capture current INF and rollback evidence",
        _ => "Capture rollback evidence before change"
    };

    private static string BuildRollbackDetail(DriverDeviceRecord device, DriverRemediationSource source)
    {
        if (source == DriverRemediationSource.MonitorOnly)
        {
            return "No rollback staging is needed while the device stays on an audit-only path.";
        }

        if (!string.IsNullOrWhiteSpace(device.InfName))
        {
            return $"Record the current INF ({device.InfName}), provider, version, signer, and instance ID, then export the audit before you touch the package source.";
        }

        return "Record the current provider, version, signer, hardware evidence, and instance ID in Device Manager and export the audit before any manual remediation.";
    }

    private static IReadOnlyList<DriverVerificationStep> BuildVerificationSteps(
        DriverDeviceRecord device,
        DriverRemediationSource source)
    {
        List<DriverVerificationStep> steps =
        [
            new(
                "Capture pre-change evidence",
                $"Record provider, version, driver date, signing state, service, class GUID, INF, and instance ID for {device.FriendlyName} before any action."),
            new(
                "Capture identifier evidence",
                device.EvidenceTier switch
                {
                    DriverEvidenceTier.HardwareBacked => "Copy the full hardware ID set and confirm the subsystem-specific ID against the intended package source.",
                    DriverEvidenceTier.CompatibleFallback => "Keep the compatible IDs in the handoff bundle and avoid trusting them as a standalone OEM match.",
                    _ => "Treat the missing hardware and compatible IDs as a blocker for any automatic source decision."
                }),
            new(
                "Choose the remediation source",
                source switch
                {
                    DriverRemediationSource.WindowsUpdateComparison => "Check Windows Update first, then compare the offered package against the current provider and the exact device evidence.",
                    DriverRemediationSource.ExactOemPackage => "Use only the exact OEM package or verified INF that matches the current hardware-backed identifiers.",
                    DriverRemediationSource.LocalInfReview => "Open the installed INF, inspect the current package details, and compare them against the OEM release before you apply any change.",
                    DriverRemediationSource.ManualTechnicianHandoff => "Escalate with the technician brief and keep the device on a manual review path until the source is proven.",
                    _ => "No source change is recommended while the device remains on a monitor-only path."
                })
        ];

        if (source != DriverRemediationSource.MonitorOnly)
        {
            steps.Add(
                new(
                    "Verify immediately after change",
                    "Re-scan the device and confirm health state, problem code, provider, version, signer, and review bucket before you close the remediation."));
        }

        if (ResolveRebootGuidance(device, source) != DriverRebootGuidance.NotExpected)
        {
            steps.Add(
                new(
                    "Verify after reboot",
                    "Restart the machine if Windows or the class driver stack requires it, then run one more scan to confirm the device remains healthy."));
        }

        return steps;
    }
}
