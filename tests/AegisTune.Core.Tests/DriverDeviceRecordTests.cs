using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class DriverDeviceRecordTests
{
    [Fact]
    public void PrioritySignals_AreDetectedForProblemAndUnsignedDrivers()
    {
        DriverDeviceRecord unsignedDevice = new(
            "USB Audio",
            "MEDIA",
            "Contoso",
            "Contoso",
            "2.4.0",
            "OK",
            0,
            "USB\\VID_1234",
            "contoso.inf",
            DateTimeOffset.Parse("2026-04-10"),
            IsSigned: false,
            SignerName: "Unknown");

        DriverDeviceRecord brokenDevice = new(
            "Wi-Fi Adapter",
            "Net",
            "Contoso",
            "Contoso",
            "5.0.1",
            "Error",
            28,
            "PCI\\VEN_9999");

        Assert.True(unsignedDevice.RequiresPriorityReview);
        Assert.Equal(RiskLevel.Risky, unsignedDevice.ReviewRiskLevel);
        Assert.Equal("Unsigned driver package", unsignedDevice.ReviewCategory);

        Assert.True(brokenDevice.RequiresPriorityReview);
        Assert.Equal(RiskLevel.Risky, brokenDevice.ReviewRiskLevel);
        Assert.Contains("Problem code", brokenDevice.ReviewCategory);
    }

    [Fact]
    public void GenericMicrosoftProvider_IsAnAdvisoryReviewForCriticalClasses()
    {
        DriverDeviceRecord device = new(
            "Bluetooth Radio",
            "Bluetooth",
            "Intel",
            "Microsoft",
            "10.0.0.1",
            "OK",
            0,
            "USB\\VID_8087");

        Assert.True(device.NeedsAttention);
        Assert.False(device.RequiresPriorityReview);
        Assert.True(device.UsesGenericProviderReview);
        Assert.Equal(RiskLevel.Review, device.ReviewRiskLevel);
        Assert.Equal("Generic Microsoft driver in critical class", device.ReviewCategory);
    }

    [Fact]
    public void Snapshot_TracksPriorityUnsignedAndHealthyCounts()
    {
        DeviceInventorySnapshot snapshot = new(
            [
                new DriverDeviceRecord(
                    "GPU",
                    "Display",
                    "NVIDIA",
                    "NVIDIA Corporation",
                    "551.0",
                    "OK",
                    0,
                    "PCI\\VEN_10DE",
                    HardwareIds:
                    [
                        "PCI\\VEN_10DE&DEV_2484&SUBSYS_40421458"
                    ]),
                new DriverDeviceRecord(
                    "Wi-Fi",
                    "Net",
                    "Intel",
                    "Microsoft",
                    "10.0.0.1",
                    "OK",
                    0,
                    "PCI\\VEN_8086",
                    HardwareIds:
                    [
                        "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
                    ]),
                new DriverDeviceRecord(
                    "Audio",
                    "MEDIA",
                    "Contoso",
                    "Contoso",
                    "2.4",
                    "OK",
                    0,
                    "HDAUDIO\\FUNC_01",
                    IsSigned: false,
                    HardwareIds:
                    [
                        "HDAUDIO\\FUNC_01&VEN_10EC&DEV_0236&SUBSYS_1028087C"
                    ]),
                new DriverDeviceRecord(
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
                    ]),
                new DriverDeviceRecord("Bluetooth", "Bluetooth", "Contoso", "", "", "Unknown", 0, "USB\\VID_0001")
            ],
            DateTimeOffset.Parse("2026-04-15T10:00:00+00:00"));

        Assert.Equal(5, snapshot.TotalDeviceCount);
        Assert.Equal(4, snapshot.NeedsAttentionCount);
        Assert.Equal(2, snapshot.PriorityReviewCount);
        Assert.Equal(2, snapshot.AdvisoryReviewCount);
        Assert.Equal(1, snapshot.UnsignedDriverCount);
        Assert.Equal(1, snapshot.GenericProviderReviewCount);
        Assert.Equal(1, snapshot.HealthyCount);
        Assert.Equal(2, snapshot.HighConfidenceMatchCount);
        Assert.Equal(2, snapshot.LowConfidenceMatchCount);
        Assert.Equal(1, snapshot.UnknownConfidenceMatchCount);
        Assert.Equal(1, snapshot.HighConfidenceOemCandidateCount);
        Assert.Equal(2, snapshot.HardwareBackedReviewCount);
        Assert.Equal(1, snapshot.CompatibleFallbackReviewCount);
        Assert.Equal(1, snapshot.NoIdentifierReviewCount);
    }

    [Fact]
    public void HardwareEvidenceLabels_ArePopulatedWhenHardwareIdsExist()
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
            DateTimeOffset.Parse("2026-04-10"),
            IsSigned: true,
            SignerName: "Microsoft Windows Hardware Compatibility Publisher",
            ClassGuid: "{4d36e972-e325-11ce-bfc1-08002be10318}",
            ServiceName: "Netwtw14",
            IsPresent: true,
            HardwareIds:
            [
                "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086",
                "PCI\\VEN_8086&DEV_43F0&CC_0280"
            ]);

        Assert.True(device.HasHardwareIds);
        Assert.Equal("2 hardware IDs", device.HardwareIdCountLabel);
        Assert.Equal("PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086", device.PrimaryHardwareId);
        Assert.Contains("PCI\\VEN_8086&DEV_43F0&CC_0280", device.HardwareIdsPreview);
        Assert.Equal("Netwtw14", device.ServiceLabel);
        Assert.Equal("Present", device.PresenceLabel);
    }

    [Fact]
    public void TechnicianHandoffSummary_IncludesDriverEvidenceFields()
    {
        DriverDeviceRecord device = new(
            "Realtek PCIe GbE",
            "Net",
            "Realtek",
            "Microsoft",
            "10.68.815.2024",
            "Error",
            31,
            "PCI\\VEN_10EC&DEV_8168",
            "rt640x64.inf",
            DateTimeOffset.Parse("2026-03-28"),
            IsSigned: false,
            SignerName: "Unknown signer",
            ClassGuid: "{4d36e972-e325-11ce-bfc1-08002be10318}",
            ServiceName: "rt640x64",
            IsPresent: true,
            HardwareIds:
            [
                "PCI\\VEN_10EC&DEV_8168&SUBSYS_E0001458"
            ]);

        Assert.Contains("Service: rt640x64", device.TechnicianHandoffSummary);
        Assert.Contains("Class GUID: {4d36e972-e325-11ce-bfc1-08002be10318}", device.TechnicianHandoffSummary);
        Assert.Contains("Match confidence:", device.TechnicianHandoffSummary);
        Assert.Contains("Primary hardware ID: PCI\\VEN_10EC&DEV_8168&SUBSYS_E0001458", device.TechnicianHandoffSummary);
        Assert.Contains("Safe source path:", device.TechnicianHandoffSummary);
    }

    [Fact]
    public void MatchConfidence_IsHighForAlignedSubsystemSpecificOemEvidence()
    {
        DriverDeviceRecord device = new(
            "Intel Wi-Fi 6 AX201",
            "Net",
            "Intel Corporation",
            "Intel",
            "23.40.0.4",
            "OK",
            0,
            "PCI\\VEN_8086&DEV_43F0",
            HardwareIds:
            [
                "PCI\\VEN_8086&DEV_43F0&SUBSYS_00748086"
            ]);

        Assert.Equal(DriverMatchConfidence.High, device.MatchConfidence);
        Assert.True(device.ProviderManufacturerAligned);
        Assert.True(device.HasSubsystemSpecificHardwareId);
        Assert.Equal("High confidence OEM match", device.MatchConfidenceLabel);
    }

    [Fact]
    public void MatchConfidence_IsLowWhenProviderLooksGeneric()
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
            HardwareIds:
            [
                "USB\\VID_8087&PID_0026&REV_0001"
            ]);

        Assert.Equal(DriverMatchConfidence.Low, device.MatchConfidence);
        Assert.True(device.ProviderLooksGeneric);
        Assert.Contains("generic or Microsoft-supplied", device.MatchConfidenceReason);
    }

    [Fact]
    public void MatchConfidence_UsesCompatibleIdsOnlyAsLowConfidenceFallback()
    {
        DriverDeviceRecord device = new(
            "MediaTek Wi-Fi",
            "Net",
            "MediaTek Inc.",
            "MediaTek",
            "3.4.2.0",
            "OK",
            0,
            "PCI\\VEN_14C3&DEV_7902",
            CompatibleIds:
            [
                "PCI\\VEN_14C3&DEV_7902",
                "PCI\\CC_028000"
            ]);

        Assert.False(device.HasHardwareIds);
        Assert.True(device.HasCompatibleIds);
        Assert.Equal(DriverMatchConfidence.Low, device.MatchConfidence);
        Assert.Equal("Compatible IDs", device.MatchEvidenceSourceLabel);
        Assert.Contains("falls back to Compatible IDs", device.MatchConfidenceReason);
    }

    [Fact]
    public void EvidenceTier_PrefersHardwareIdsWhenBothIdentifierSetsExist()
    {
        DriverDeviceRecord device = new(
            "Realtek NIC",
            "Net",
            "Realtek",
            "Realtek",
            "2.0.0",
            "OK",
            0,
            "PCI\\VEN_10EC&DEV_8168",
            HardwareIds:
            [
                "PCI\\VEN_10EC&DEV_8168&SUBSYS_E0001458"
            ],
            CompatibleIds:
            [
                "PCI\\VEN_10EC&DEV_8168"
            ]);

        Assert.Equal(DriverEvidenceTier.HardwareBacked, device.EvidenceTier);
        Assert.Equal("Hardware-backed evidence", device.EvidenceTierLabel);
    }

    [Fact]
    public void MatchConfidence_RemainsUnknownWhenNoIdentifiersExist()
    {
        DriverDeviceRecord device = new(
            "Unknown device",
            "System",
            "Unknown vendor",
            "",
            "",
            "Unknown",
            0,
            "ROOT\\UNKNOWN\\0000");

        Assert.False(device.HasMatchEvidence);
        Assert.Equal(DriverMatchConfidence.Unknown, device.MatchConfidence);
    }

    [Fact]
    public void VendorAliasNormalization_SupportsCommonWirelessVendors()
    {
        DriverDeviceRecord device = new(
            "Qualcomm Adapter",
            "Net",
            "Qualcomm Atheros",
            "Qualcomm",
            "1.0.0",
            "OK",
            0,
            "PCI\\VEN_168C&DEV_003E",
            HardwareIds:
            [
                "PCI\\VEN_168C&DEV_003E&SUBSYS_217F1A3B"
            ]);

        Assert.True(device.ProviderManufacturerAligned);
        Assert.Equal(DriverMatchConfidence.High, device.MatchConfidence);
    }
}
