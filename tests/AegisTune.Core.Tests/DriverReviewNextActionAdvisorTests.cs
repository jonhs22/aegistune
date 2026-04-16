using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class DriverReviewNextActionAdvisorTests
{
    [Fact]
    public void Create_UsesInstallLaneWhenLocalCandidateExists()
    {
        DriverDeviceRecord device = new(
            "Intel Wi-Fi 6 AX201",
            "Net",
            "Intel",
            "Intel",
            "23.40.0.4",
            "OK",
            0,
            "PCI\\VEN_8086&DEV_43F0",
            "netwtw14.inf",
            HardwareIds:
            [
                "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
            ]);

        DriverRepositoryCandidate candidate = new(
            @"F:\Drivers\netwtw14.inf",
            @"F:\Drivers",
            "Intel",
            "Net",
            "23.40.0.4",
            "netwtw14.cat",
            DriverRepositoryMatchKind.ExactHardwareId,
            [
                "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
            ]);

        DriverReviewNextActionGuidance guidance = DriverReviewNextActionAdvisor.Create(device, candidate, hasRepositoryRoots: true);

        Assert.Equal(DriverReviewNextActionKind.InstallSelectedLocalDriver, guidance.PrimaryActionKind);
        Assert.Equal("Install selected local driver", guidance.PrimaryActionLabel);
        Assert.Equal(DriverReviewNextActionKind.OpenSelectedInf, guidance.SecondaryActionKind);
    }

    [Fact]
    public void Create_UsesDeviceManagerForPriorityReviewWithoutCandidate()
    {
        DriverDeviceRecord device = new(
            "Unknown USB Device",
            "USB",
            "Microsoft",
            "Microsoft",
            "10.0.0.1",
            "Error",
            43,
            "USB\\VID_0000");

        DriverReviewNextActionGuidance guidance = DriverReviewNextActionAdvisor.Create(device, selectedCandidate: null, hasRepositoryRoots: true);

        Assert.Equal(DriverReviewNextActionKind.OpenDeviceManager, guidance.PrimaryActionKind);
        Assert.Equal(DriverReviewNextActionKind.CopyTechnicianBrief, guidance.SecondaryActionKind);
    }

    [Fact]
    public void Create_UsesSettingsWhenRepositoryRootsAreMissing()
    {
        DriverDeviceRecord device = new(
            "Intel Bluetooth",
            "Bluetooth",
            "Intel",
            "Intel",
            "23.40.0.4",
            "OK",
            0,
            "USB\\VID_8087",
            "ibtusb.inf",
            HardwareIds:
            [
                "USB\\VID_8087&PID_0032&REV_0001"
            ]);

        DriverReviewNextActionGuidance guidance = DriverReviewNextActionAdvisor.Create(device, selectedCandidate: null, hasRepositoryRoots: false);

        Assert.Equal(DriverReviewNextActionKind.OpenSettings, guidance.PrimaryActionKind);
        Assert.Equal(DriverReviewNextActionKind.CopyTechnicianBrief, guidance.SecondaryActionKind);
    }
}
