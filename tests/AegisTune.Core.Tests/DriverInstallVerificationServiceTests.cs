using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class DriverInstallVerificationServiceTests
{
    [Fact]
    public void Verify_WhenDriverFingerprintChanges_ReturnsDriverChanged()
    {
        DriverInstallVerificationService service = new();
        DriverDeviceRecord before = CreateDevice(
            provider: "Microsoft",
            version: "1.0.0.0",
            status: "OK",
            problemCode: 0,
            infName: "oem10.inf",
            signer: "Microsoft Windows");
        DriverDeviceRecord after = before with
        {
            DriverProvider = "Realtek",
            DriverVersion = "2.5.0.0",
            InfName = "oem55.inf",
            SignerName = "Realtek Semiconductor Corp."
        };

        DriverInstallVerificationResult result = service.Verify(before, after, CreateCandidate());

        Assert.Equal(DriverInstallVerificationOutcome.DriverChanged, result.Outcome);
        Assert.Contains("Provider", result.ChangedFields);
        Assert.Contains("Version", result.ChangedFields);
        Assert.Contains("INF", result.ChangedFields);
        Assert.Contains("Signer", result.ChangedFields);
    }

    [Fact]
    public void Verify_WhenProblemCodeAndStatusImprove_ReturnsDeviceImproved()
    {
        DriverInstallVerificationService service = new();
        DriverDeviceRecord before = CreateDevice(
            provider: "Contoso",
            version: "1.0.0.0",
            status: "Error",
            problemCode: 28,
            infName: "oem42.inf",
            isSigned: false,
            signer: "Unknown signer");
        DriverDeviceRecord after = before with
        {
            DeviceStatus = "OK",
            ProblemCode = 0,
            IsSigned = true
        };

        DriverInstallVerificationResult result = service.Verify(before, after, CreateCandidate());

        Assert.Equal(DriverInstallVerificationOutcome.DeviceImproved, result.Outcome);
        Assert.Contains("Status", result.ChangedFields);
        Assert.Contains("Signing", result.ChangedFields);
        Assert.Contains("Problem code", result.ChangedFields);
    }

    [Fact]
    public void Verify_WhenNothingChanges_ReturnsNoChange()
    {
        DriverInstallVerificationService service = new();
        DriverDeviceRecord before = CreateDevice(
            provider: "Contoso",
            version: "1.0.0.0",
            status: "OK",
            problemCode: 0,
            infName: "oem42.inf");

        DriverInstallVerificationResult result = service.Verify(before, before with { }, CreateCandidate());

        Assert.Equal(DriverInstallVerificationOutcome.NoChange, result.Outcome);
        Assert.Empty(result.ChangedFields);
    }

    [Fact]
    public void Verify_WhenDeviceIsMissingAfterRefresh_ReturnsInconclusive()
    {
        DriverInstallVerificationService service = new();
        DriverDeviceRecord before = CreateDevice(
            provider: "Contoso",
            version: "1.0.0.0",
            status: "Error",
            problemCode: 31,
            infName: "oem42.inf");

        DriverInstallVerificationResult result = service.Verify(before, null, CreateCandidate());

        Assert.Equal(DriverInstallVerificationOutcome.VerificationInconclusive, result.Outcome);
        Assert.Contains("Device missing after refresh", result.ChangedFields);
    }

    private static DriverRepositoryCandidate CreateCandidate() =>
        new(
            @"C:\drivers\contoso\device.inf",
            @"C:\drivers\contoso",
            "Contoso",
            "Net",
            "2.5.0.0",
            "device.cat",
            DriverRepositoryMatchKind.ExactHardwareId,
            ["PCI\\VEN_1234&DEV_5678"]);

    private static DriverDeviceRecord CreateDevice(
        string provider,
        string version,
        string status,
        int problemCode,
        string infName,
        bool? isSigned = true,
        string signer = "Contoso Signing")
    {
        return new DriverDeviceRecord(
            "Contoso Wi-Fi",
            "Net",
            "Contoso",
            provider,
            version,
            status,
            problemCode,
            "PCI\\VEN_1234&DEV_5678",
            InfName: infName,
            IsSigned: isSigned,
            SignerName: signer,
            HardwareIds:
            [
                "PCI\\VEN_1234&DEV_5678&SUBSYS_00011234"
            ]);
    }
}
