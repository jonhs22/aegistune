using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class DriverRemediationPlannerTests
{
    [Fact]
    public void Build_HighConfidencePriorityDevice_PrefersExactOemPackage()
    {
        DriverDeviceRecord device = new(
            "Intel Wi-Fi 6 AX201",
            "Net",
            "Intel",
            "Intel",
            "23.40.0.4",
            "Error",
            10,
            "PCI\\VEN_8086&DEV_43F0",
            "netwtw14.inf",
            DateTimeOffset.Parse("2026-04-10"),
            IsSigned: true,
            SignerName: "Microsoft Windows Hardware Compatibility Publisher",
            ClassGuid: "{4d36e972-e325-11ce-bfc1-08002be10318}",
            ServiceName: "Netwtw14",
            IsPresent: true,
            HardwareIds:
            [
                "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
            ]);

        DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);

        Assert.Equal(DriverRemediationSource.ExactOemPackage, plan.RecommendedSource);
        Assert.Equal(DriverRebootGuidance.LikelyRequired, plan.RebootGuidance);
        Assert.Contains("exact OEM", plan.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.True(plan.VerificationSteps.Count >= 4);
    }

    [Fact]
    public void Build_CompatibleFallbackDevice_StaysOnManualHandoff()
    {
        DriverDeviceRecord device = new(
            "MediaTek Wi-Fi",
            "Net",
            "MediaTek",
            "MediaTek",
            "3.4",
            "OK",
            0,
            "PCI\\VEN_14C3",
            CompatibleIds:
            [
                "PCI\\VEN_14C3&DEV_7902",
                "PCI\\CC_028000"
            ]);

        DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);

        Assert.Equal(DriverRemediationSource.ManualTechnicianHandoff, plan.RecommendedSource);
        Assert.Contains("manual review", plan.SourceReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_GenericMicrosoftCriticalClass_PrefersWindowsUpdateComparison()
    {
        DriverDeviceRecord device = new(
            "Bluetooth Radio",
            "Bluetooth",
            "Intel",
            "Microsoft",
            "10.0.0.1",
            "OK",
            0,
            "USB\\VID_8087",
            "bth.inf",
            DateTimeOffset.Parse("2026-04-10"),
            IsSigned: true,
            SignerName: "Microsoft Windows",
            ClassGuid: "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}",
            ServiceName: "BTHUSB",
            IsPresent: true,
            HardwareIds:
            [
                "USB\\VID_8087&PID_0026&REV_0001"
            ]);

        DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);

        Assert.Equal(DriverRemediationSource.WindowsUpdateComparison, plan.RecommendedSource);
        Assert.Equal(DriverRebootGuidance.MayBeRequired, plan.RebootGuidance);
    }
}
