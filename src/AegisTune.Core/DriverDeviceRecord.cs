namespace AegisTune.Core;

public sealed record DriverDeviceRecord(
    string FriendlyName,
    string DeviceClass,
    string Manufacturer,
    string DriverProvider,
    string DriverVersion,
    string DeviceStatus,
    int ProblemCode,
    string InstanceId,
    string? InfName = null,
    DateTimeOffset? DriverDate = null,
    bool? IsSigned = null,
    string? SignerName = null,
    string? ClassGuid = null,
    string? ServiceName = null,
    bool? IsPresent = null,
    IReadOnlyList<string>? HardwareIds = null,
    IReadOnlyList<string>? CompatibleIds = null)
{
    public bool IsCriticalClass =>
        DeviceClass is not null
        && CriticalClasses.Contains(DeviceClass);

    public bool UsesMicrosoftProvider =>
        !string.IsNullOrWhiteSpace(DriverProvider)
        && DriverProvider.Contains("microsoft", StringComparison.OrdinalIgnoreCase);

    public bool HasSigningConcern => IsSigned == false;

    public bool UsesGenericProviderReview =>
        UsesMicrosoftProvider
        && IsCriticalClass
        && !Manufacturer.Contains("microsoft", StringComparison.OrdinalIgnoreCase);

    public bool RequiresPriorityReview =>
        ProblemCode != 0
        || !string.Equals(DeviceStatus, "OK", StringComparison.OrdinalIgnoreCase)
        || HasSigningConcern;

    public bool NeedsAttention =>
        RequiresPriorityReview
        || string.IsNullOrWhiteSpace(DriverProvider)
        || UsesGenericProviderReview
        || EvidenceTier == DriverEvidenceTier.CompatibleFallback;

    public string ProviderLabel => string.IsNullOrWhiteSpace(DriverProvider) ? "Provider unknown" : DriverProvider;

    public string VersionLabel => string.IsNullOrWhiteSpace(DriverVersion) ? "Version unknown" : DriverVersion;

    public string DriverDateLabel => DriverDate?.ToLocalTime().ToString("d") ?? "Date unknown";

    public string SigningLabel => IsSigned switch
    {
        true => "Signed",
        false => "Unsigned",
        _ => "Signature unknown"
    };

    public string SignerLabel => string.IsNullOrWhiteSpace(SignerName) ? "Signer unknown" : SignerName;

    public string InfLabel => string.IsNullOrWhiteSpace(InfName) ? "INF file unknown" : InfName;

    public string ClassGuidLabel => string.IsNullOrWhiteSpace(ClassGuid) ? "Class GUID unavailable" : ClassGuid;

    public string ServiceLabel => string.IsNullOrWhiteSpace(ServiceName) ? "Service unavailable" : ServiceName;

    public string PresenceLabel => IsPresent switch
    {
        true => "Present",
        false => "Not currently present",
        _ => "Presence unknown"
    };

    public bool HasHardwareIdEvidence => HasHardwareIds;

    public bool HasHardwareIds => HardwareIds?.Count > 0;

    public string PrimaryHardwareId => HasHardwareIds ? HardwareIds![0] : "Hardware ID unavailable";

    public string HardwareIdCountLabel => HasHardwareIds
        ? $"{HardwareIds!.Count:N0} hardware ID{(HardwareIds.Count == 1 ? string.Empty : "s")}"
        : "Hardware IDs unavailable";

    public string HardwareIdsPreview => HasHardwareIds
        ? string.Join(Environment.NewLine, HardwareIds!.Take(5))
        : "No hardware IDs reported by Windows for this device.";

    public bool HasCompatibleIdEvidence => HasCompatibleIds;

    public bool HasCompatibleIds => CompatibleIds?.Count > 0;

    public string PrimaryCompatibleId => HasCompatibleIds ? CompatibleIds![0] : "Compatible ID unavailable";

    public string CompatibleIdCountLabel => HasCompatibleIds
        ? $"{CompatibleIds!.Count:N0} compatible ID{(CompatibleIds.Count == 1 ? string.Empty : "s")}"
        : "Compatible IDs unavailable";

    public string CompatibleIdsPreview => HasCompatibleIds
        ? string.Join(Environment.NewLine, CompatibleIds!.Take(5))
        : "No compatible IDs reported by Windows for this device.";

    public bool HasMatchEvidence => HasHardwareIds || HasCompatibleIds;

    public string PrimaryMatchEvidence => HasHardwareIds
        ? PrimaryHardwareId
        : HasCompatibleIds
            ? PrimaryCompatibleId
            : "Match evidence unavailable";

    public string MatchEvidenceSourceLabel => HasHardwareIds
        ? "Hardware IDs"
        : HasCompatibleIds
            ? "Compatible IDs"
            : "No identifier evidence";

    public DriverEvidenceTier EvidenceTier => HasHardwareIds
        ? DriverEvidenceTier.HardwareBacked
        : HasCompatibleIds
            ? DriverEvidenceTier.CompatibleFallback
            : DriverEvidenceTier.NoIdentifierEvidence;

    public string EvidenceTierLabel => EvidenceTier switch
    {
        DriverEvidenceTier.HardwareBacked => "Hardware-backed evidence",
        DriverEvidenceTier.CompatibleFallback => "Compatible-fallback evidence",
        _ => "No identifier evidence"
    };

    public string EvidenceTierDescription => EvidenceTier switch
    {
        DriverEvidenceTier.HardwareBacked => "Hardware IDs are available, so this device can be compared against an exact OEM package or INF path.",
        DriverEvidenceTier.CompatibleFallback => "Windows exposed only Compatible IDs. Keep this on a technician review path instead of treating it as a trusted OEM match.",
        _ => "Windows did not expose hardware IDs or compatible IDs for this device. Do not trust an automated package path."
    };

    public string ReviewQueueEvidenceLabel => EvidenceTier switch
    {
        DriverEvidenceTier.HardwareBacked => HardwareIdCountLabel,
        DriverEvidenceTier.CompatibleFallback => CompatibleIdCountLabel,
        _ => "No hardware or compatible IDs"
    };

    public bool HasSubsystemSpecificHardwareId =>
        HasHardwareIds
        && HardwareIds!.Any(static id => SpecificHardwareEvidenceTokens.Any(token => id.Contains(token, StringComparison.OrdinalIgnoreCase)));

    public bool HasSpecificCompatibleId =>
        HasCompatibleIds
        && CompatibleIds!.Any(static id => SpecificHardwareEvidenceTokens.Any(token => id.Contains(token, StringComparison.OrdinalIgnoreCase)));

    public bool ProviderLooksGeneric
    {
        get
        {
            string provider = NormalizeVendorToken(DriverProvider);
            return string.IsNullOrWhiteSpace(provider)
                || GenericProviderTokens.Contains(provider);
        }
    }

    public bool ProviderManufacturerAligned
    {
        get
        {
            string provider = NormalizeVendorToken(DriverProvider);
            string manufacturer = NormalizeVendorToken(Manufacturer);

            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(manufacturer) || ProviderLooksGeneric)
            {
                return false;
            }

            return provider == manufacturer
                || provider.Contains(manufacturer, StringComparison.Ordinal)
                || manufacturer.Contains(provider, StringComparison.Ordinal);
        }
    }

    public bool ProviderManufacturerMismatch
    {
        get
        {
            string provider = NormalizeVendorToken(DriverProvider);
            string manufacturer = NormalizeVendorToken(Manufacturer);

            return !string.IsNullOrWhiteSpace(provider)
                && !string.IsNullOrWhiteSpace(manufacturer)
                && !ProviderLooksGeneric
                && !ProviderManufacturerAligned;
        }
    }

    public DriverMatchConfidence MatchConfidence => HasMatchEvidence switch
    {
        false => DriverMatchConfidence.Unknown,
        _ when HasHardwareIds && ProviderManufacturerAligned && !ProviderLooksGeneric && HasSubsystemSpecificHardwareId => DriverMatchConfidence.High,
        _ when HasHardwareIds && ProviderManufacturerAligned && !ProviderLooksGeneric => DriverMatchConfidence.Medium,
        _ when HasCompatibleIds => DriverMatchConfidence.Low,
        true when ProviderLooksGeneric || ProviderManufacturerMismatch => DriverMatchConfidence.Low,
        _ => DriverMatchConfidence.Unknown
    };

    public string MatchConfidenceLabel => MatchConfidence switch
    {
        DriverMatchConfidence.High => "High confidence OEM match",
        DriverMatchConfidence.Medium => "Medium confidence OEM match",
        DriverMatchConfidence.Low => "Low confidence OEM match",
        _ => "OEM match unknown"
    };

    public string MatchConfidenceReason
    {
        get
        {
            return MatchConfidence switch
            {
                DriverMatchConfidence.High => "Hardware IDs include subsystem or model-specific evidence and the driver provider aligns with the reported manufacturer.",
                DriverMatchConfidence.Medium => "Hardware IDs exist and the driver provider aligns with the reported manufacturer, but model-specific evidence is limited.",
                DriverMatchConfidence.Low when !HasHardwareIds && HasCompatibleIds => "Hardware IDs are missing, so this falls back to Compatible IDs only. Keep it on a review path instead of treating it as a trusted OEM match.",
                DriverMatchConfidence.Low when ProviderLooksGeneric => $"{MatchEvidenceSourceLabel} exist, but the current provider looks generic or Microsoft-supplied rather than model-specific OEM evidence.",
                DriverMatchConfidence.Low when ProviderManufacturerMismatch => $"{MatchEvidenceSourceLabel} exist, but the current provider does not align with the reported manufacturer.",
                DriverMatchConfidence.Low => $"{MatchEvidenceSourceLabel} exist, but the available provider evidence is still too weak for an automatic OEM package path.",
                _ => $"Hardware IDs or compatible IDs are too incomplete to trust an OEM package path yet."
            };
        }
    }

    public bool IsHighConfidenceOemCandidate =>
        NeedsAttention
        && MatchConfidence == DriverMatchConfidence.High;

    public string ClassPriorityLabel => IsCriticalClass ? "Critical hardware class" : "Standard hardware class";

    public RiskLevel ReviewRiskLevel => RequiresPriorityReview
        ? RiskLevel.Risky
        : NeedsAttention
            ? RiskLevel.Review
            : RiskLevel.Safe;

    public string ReviewBucketLabel => ReviewRiskLevel switch
    {
        RiskLevel.Risky => "Priority review",
        RiskLevel.Review => "Advisory review",
        _ => "Healthy"
    };

    public string ReviewCategory => ProblemCode switch
    {
        not 0 => "Problem code present",
        _ when !string.Equals(DeviceStatus, "OK", StringComparison.OrdinalIgnoreCase) => "Device status mismatch",
        _ when HasSigningConcern => "Unsigned driver package",
        _ when string.IsNullOrWhiteSpace(DriverProvider) => "Driver provider missing",
        _ when UsesGenericProviderReview => "Generic Microsoft driver in critical class",
        _ => "Healthy"
    };

    public string RecommendedAction
    {
        get
        {
            if (ReviewRiskLevel == RiskLevel.Safe)
            {
                return "No corrective action is currently indicated. Keep this device in the audit trail and re-check after major Windows updates.";
            }

            if (ReviewRiskLevel == RiskLevel.Risky && MatchConfidence == DriverMatchConfidence.High)
            {
                return $"Use Device Manager to confirm the current failure state, then compare only the exact OEM package or INF that matches this {MatchEvidenceSourceLabel.ToLowerInvariant()} evidence. Keep rollback evidence explicit.";
            }

            if (ReviewRiskLevel == RiskLevel.Risky)
            {
                return "Use Device Manager first and keep this on a technician handoff path until the hardware IDs, compatible IDs, provider, and model evidence agree. Do not jump straight to an OEM package.";
            }

            if (EvidenceTier == DriverEvidenceTier.CompatibleFallback)
            {
                return "Windows exposed only Compatible IDs for this device. Keep it on a technician handoff path until fuller hardware ID evidence is captured.";
            }

            if (UsesGenericProviderReview && MatchConfidence == DriverMatchConfidence.High)
            {
                return $"The current package looks generic, but the {MatchEvidenceSourceLabel.ToLowerInvariant()} evidence points to a strong OEM match. Compare the Microsoft-supplied driver with the exact OEM release for this device before changing anything.";
            }

            return MatchConfidence switch
            {
                DriverMatchConfidence.High => "Driver provider and manufacturer align. If correction is needed, use only the exact OEM package or verified INF for this device, based on the current hardware ID evidence.",
                DriverMatchConfidence.Medium => "Provider and manufacturer align, but the model evidence is incomplete. Verify the exact subsystem before using an OEM package, even with the current hardware ID evidence.",
                DriverMatchConfidence.Low => "Current identifier evidence is too generic or vendor-mismatched. Keep this on a technician handoff path instead of trusting an OEM package automatically.",
                _ => "Collect fuller hardware IDs or compatible IDs and vendor evidence before treating Windows Update or an OEM package as a safe source."
            };
        }
    }

    public string HealthLabel => ProblemCode switch
    {
        not 0 => $"Problem code {ProblemCode}",
        _ when !string.Equals(DeviceStatus, "OK", StringComparison.OrdinalIgnoreCase) => DeviceStatus,
        _ when HasSigningConcern => "Unsigned driver",
        _ when string.IsNullOrWhiteSpace(DriverProvider) => "Driver provider needs review",
        _ when UsesGenericProviderReview => "Review Microsoft-supplied driver",
        _ => "Healthy"
    };

    public string SafeSourcePath
    {
        get
        {
            if (ReviewRiskLevel == RiskLevel.Safe)
            {
                return "No remediation source path is currently required.";
            }

            if (ReviewRiskLevel == RiskLevel.Risky && MatchConfidence == DriverMatchConfidence.High)
            {
                return $"Start from Device Manager, then prefer the exact OEM support package or matching INF because the {MatchEvidenceSourceLabel.ToLowerInvariant()} and provider line up strongly.";
            }

            if (ReviewRiskLevel == RiskLevel.Risky)
            {
                return "Start from Device Manager, but keep this on a manual handoff path until the hardware IDs, compatible IDs, provider, and model evidence agree.";
            }

            if (EvidenceTier == DriverEvidenceTier.CompatibleFallback)
            {
                return "Stay on a manual handoff path. Compatible IDs alone are not enough to trust an OEM package automatically.";
            }

            if (UsesGenericProviderReview && MatchConfidence == DriverMatchConfidence.High)
            {
                return $"Compare the current Microsoft-supplied driver with the exact OEM package for this subsystem. Prefer the OEM path only if the current {MatchEvidenceSourceLabel.ToLowerInvariant()} and release notes match.";
            }

            return MatchConfidence switch
            {
                DriverMatchConfidence.High => "Current hardware IDs and provider align. Prefer the exact OEM support package or matching INF only after confirming the model.",
                DriverMatchConfidence.Medium => "Provider aligns with the manufacturer, but model evidence is incomplete. Verify the exact subsystem before taking an OEM package path, even with the current hardware ID evidence.",
                DriverMatchConfidence.Low => "Stay on a manual handoff path. Current identifier evidence is generic or mismatched, so no package source should be trusted automatically.",
                _ => "Collect fuller hardware IDs or compatible IDs and vendor evidence before choosing Windows Update or any OEM source path."
            };
        }
    }

    public string TechnicianHandoffSummary =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"Device: {FriendlyName}",
                $"Review bucket: {ReviewBucketLabel}",
                $"Category: {ReviewCategory}",
                $"Class: {DeviceClass}",
                $"Manufacturer: {Manufacturer}",
                $"Provider: {ProviderLabel}",
                $"Version: {VersionLabel}",
                $"Driver date: {DriverDateLabel}",
                $"Signing: {SigningLabel}",
                $"Signer: {SignerLabel}",
                $"Service: {ServiceLabel}",
                $"Presence: {PresenceLabel}",
                $"Class GUID: {ClassGuidLabel}",
                $"Evidence tier: {EvidenceTierLabel}",
                $"Match evidence source: {MatchEvidenceSourceLabel}",
                $"Match confidence: {MatchConfidenceLabel}",
                $"Match reason: {MatchConfidenceReason}",
                $"Primary hardware ID: {PrimaryHardwareId}",
                $"Primary compatible ID: {PrimaryCompatibleId}",
                $"INF: {InfLabel}",
                $"Instance ID: {InstanceId}",
                $"Safe source path: {SafeSourcePath}",
                $"Recommended action: {RecommendedAction}"
            });

    private static readonly HashSet<string> CriticalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bluetooth",
        "Camera",
        "Display",
        "HDC",
        "MEDIA",
        "Net",
        "SCSIAdapter",
        "System",
        "USB"
    };

    private static readonly HashSet<string> GenericProviderTokens = new(StringComparer.Ordinal)
    {
        "generic",
        "microsoft",
        "standard",
        "unknown",
        "windows"
    };

    private static readonly string[] SpecificHardwareEvidenceTokens =
    [
        "SUBSYS_",
        "SSID_",
        "REV_",
        "MI_"
    ];

    private static readonly string[] VendorNoiseTokens =
    [
        "corporation",
        "corp",
        "company",
        "co",
        "inc",
        "ltd",
        "limited",
        "technologies",
        "technology",
        "semiconductor",
        "semiconductors",
        "electronics",
        "systems"
    ];

    private static readonly Dictionary<string, string> VendorAliases = new(StringComparer.Ordinal)
    {
        ["intelcorporation"] = "intel",
        ["advancedmicrodevices"] = "amd",
        ["advancedmicrodevicesinc"] = "amd",
        ["nvidiacorporation"] = "nvidia",
        ["realteksemiconductor"] = "realtek",
        ["realteksemiconductorcorp"] = "realtek",
        ["asustekcomputer"] = "asus",
        ["asustekcomputerinc"] = "asus",
        ["microstarinternational"] = "msi",
        ["microstarinternationalco"] = "msi",
        ["qualcommatheros"] = "qualcomm",
        ["mediatekinc"] = "mediatek",
        ["broadcominc"] = "broadcom"
    };

    private static string NormalizeVendorToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.ToLowerInvariant();
        foreach (string noiseToken in VendorNoiseTokens)
        {
            normalized = normalized.Replace(noiseToken, string.Empty, StringComparison.Ordinal);
        }

        normalized = new string(normalized.Where(char.IsLetterOrDigit).ToArray());
        return VendorAliases.TryGetValue(normalized, out string? alias)
            ? alias
            : normalized;
    }
}
