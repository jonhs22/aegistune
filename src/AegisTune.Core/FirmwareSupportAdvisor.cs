namespace AegisTune.Core;

public static class FirmwareSupportAdvisor
{
    public static FirmwareInventorySnapshot Build(
        string? systemManufacturer,
        string? systemModel,
        string? baseboardManufacturer,
        string? baseboardProduct,
        string? biosManufacturer,
        string? biosVersion,
        string? biosFamilyVersion,
        DateTimeOffset? biosReleaseDate,
        string? firmwareMode,
        bool? secureBootEnabled,
        DateTimeOffset collectedAt,
        string? warningMessage = null)
    {
        string normalizedSystemManufacturer = NormalizeDisplayValue(systemManufacturer, "Unknown system manufacturer");
        string normalizedSystemModel = NormalizeDisplayValue(systemModel, "Unknown system model");
        string normalizedBoardManufacturer = NormalizeDisplayValue(baseboardManufacturer, "Unknown baseboard vendor");
        string normalizedBoardProduct = NormalizeDisplayValue(baseboardProduct, "Unknown baseboard product");
        string normalizedBiosManufacturer = NormalizeDisplayValue(biosManufacturer, "Unknown BIOS vendor");
        string normalizedBiosVersion = NormalizeDisplayValue(biosVersion, "Unknown BIOS version");
        string normalizedBiosFamilyVersion = NormalizeDisplayValue(biosFamilyVersion, "Unknown firmware family version");
        string normalizedFirmwareMode = NormalizeFirmwareMode(firmwareMode);

        bool hasUsableSystemIdentity = IsUsefulIdentityPart(systemManufacturer) && IsUsefulIdentityPart(systemModel);
        bool hasUsableBoardIdentity = IsUsefulIdentityPart(baseboardManufacturer) && IsUsefulIdentityPart(baseboardProduct);

        string supportManufacturer = hasUsableSystemIdentity
            ? normalizedSystemManufacturer
            : hasUsableBoardIdentity
                ? normalizedBoardManufacturer
                : IsUsefulIdentityPart(systemManufacturer)
                    ? normalizedSystemManufacturer
                    : IsUsefulIdentityPart(baseboardManufacturer)
                        ? normalizedBoardManufacturer
                        : normalizedSystemManufacturer;

        string supportModel = hasUsableSystemIdentity
            ? normalizedSystemModel
            : hasUsableBoardIdentity
                ? normalizedBoardProduct
                : IsUsefulIdentityPart(systemModel)
                    ? normalizedSystemModel
                    : IsUsefulIdentityPart(baseboardProduct)
                        ? normalizedBoardProduct
                        : normalizedSystemModel;

        string supportIdentitySourceLabel = hasUsableSystemIdentity
            ? "System model identity"
            : hasUsableBoardIdentity
                ? "Baseboard fallback identity"
                : "Generic machine identity";

        string supportVendorKey = NormalizeVendorKey(supportManufacturer);
        FirmwareSupportRoute route = SupportRoutes.TryGetValue(supportVendorKey, out FirmwareSupportRoute? mappedRoute)
            ? mappedRoute
            : new FirmwareSupportRoute(
                string.IsNullOrWhiteSpace(supportManufacturer) ? "Vendor support" : supportManufacturer,
                null,
                "Manual firmware review route");

        string supportManufacturerLabel = route.DisplayName;
        string supportSearchHint = BuildSearchHint(supportModel, normalizedBiosVersion, route.DisplayName);
        string readinessSummary = BuildReadinessSummary(
            supportIdentitySourceLabel,
            route,
            supportModel,
            normalizedBiosVersion,
            biosReleaseDate,
            normalizedFirmwareMode);
        FirmwareSupportOption[] options = BuildSupportOptions(
            supportIdentitySourceLabel,
            route,
            supportModel,
            normalizedBiosVersion,
            biosReleaseDate,
            normalizedFirmwareMode,
            secureBootEnabled);

        return new FirmwareInventorySnapshot(
            normalizedSystemManufacturer,
            normalizedSystemModel,
            normalizedBoardManufacturer,
            normalizedBoardProduct,
            normalizedBiosManufacturer,
            normalizedBiosVersion,
            normalizedBiosFamilyVersion,
            biosReleaseDate,
            normalizedFirmwareMode,
            secureBootEnabled,
            supportManufacturerLabel,
            supportModel,
            supportIdentitySourceLabel,
            route.RouteLabel,
            route.SupportUrl,
            supportSearchHint,
            readinessSummary,
            options,
            collectedAt,
            warningMessage);
    }

    private static FirmwareSupportOption[] BuildSupportOptions(
        string supportIdentitySourceLabel,
        FirmwareSupportRoute route,
        string supportModel,
        string biosVersion,
        DateTimeOffset? biosReleaseDate,
        string firmwareMode,
        bool? secureBootEnabled)
    {
        string releaseEvidence = biosReleaseDate is null
            ? $"Current BIOS version {biosVersion} was captured without a release date from Windows."
            : $"Current BIOS version {biosVersion} was released on {biosReleaseDate.Value.ToLocalTime():d}.";

        string sourceDetail = supportIdentitySourceLabel == "Baseboard fallback identity"
            ? $"This machine exposes generic OEM strings, so AegisTune is routing firmware review through the board model {supportModel}."
            : $"Use the exact support page for {supportModel} and compare the current firmware against the vendor release notes before flashing.";

        return
        [
            new FirmwareSupportOption(
                route.SupportUrl is null ? "Manual vendor support lookup" : "Open official firmware support",
                route.SupportUrl is null
                    ? $"No direct OEM route is mapped yet. {sourceDetail}"
                    : $"{sourceDetail} Route: {route.SupportUrl}"),
            new FirmwareSupportOption(
                "Cross-check Windows Update",
                "Review Windows Update after the OEM release notes are checked, but do not let Windows alone decide the firmware path for a business machine."),
            new FirmwareSupportOption(
                "Capture the current baseline",
                $"{releaseEvidence} Firmware mode is {firmwareMode} and Secure Boot is {BuildSecureBootPhrase(secureBootEnabled)}."),
            new FirmwareSupportOption(
                "Apply flash safety gates",
                "Before any BIOS flash, keep AC power or UPS connected, preserve a rollback path, and verify the board or chassis model one more time.")
        ];
    }

    private static string BuildReadinessSummary(
        string supportIdentitySourceLabel,
        FirmwareSupportRoute route,
        string supportModel,
        string biosVersion,
        DateTimeOffset? biosReleaseDate,
        string firmwareMode)
    {
        string identityClause = supportIdentitySourceLabel == "Baseboard fallback identity"
            ? $"Windows exposed generic chassis strings, so firmware review is using the board model {supportModel}."
            : $"Firmware review is aligned to {supportModel}.";

        string routeClause = route.SupportUrl is null
            ? "No direct OEM route is mapped yet, so keep this on a manual vendor-review path."
            : $"{route.DisplayName} support is the preferred route for release-note verification.";

        string dateClause = biosReleaseDate is null
            ? $"Current BIOS {biosVersion} is present, but Windows did not return a release date."
            : $"Current BIOS {biosVersion} was released on {biosReleaseDate.Value.ToLocalTime():d}.";

        string modeClause = firmwareMode switch
        {
            "Legacy BIOS" => "Legacy BIOS was detected, so do not assume UEFI-era firmware guidance.",
            "UEFI" => "UEFI firmware was detected.",
            _ => "Firmware mode could not be confirmed from Windows."
        };

        return $"{identityClause} {routeClause} {dateClause} {modeClause} Compare the vendor release notes before any BIOS update.";
    }

    private static string BuildSearchHint(string supportModel, string biosVersion, string vendorName)
    {
        string query = string.IsNullOrWhiteSpace(biosVersion) || biosVersion.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            ? $"{supportModel} BIOS"
            : $"{supportModel} BIOS {biosVersion}";

        return $"Search the official {vendorName} support page for \"{query}\" before any firmware change.";
    }

    private static string BuildSecureBootPhrase(bool? secureBootEnabled) => secureBootEnabled switch
    {
        true => "enabled",
        false => "off or unavailable",
        _ => "unknown"
    };

    private static string NormalizeDisplayValue(string? value, string fallbackLabel)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackLabel;
        }

        string normalized = string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? fallbackLabel : normalized;
    }

    private static string NormalizeFirmwareMode(string? firmwareMode)
    {
        if (string.IsNullOrWhiteSpace(firmwareMode))
        {
            return "Firmware mode unknown";
        }

        return firmwareMode.Trim() switch
        {
            "UEFI" => "UEFI",
            "Legacy BIOS" => "Legacy BIOS",
            _ => firmwareMode.Trim()
        };
    }

    private static bool IsUsefulIdentityPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return PlaceholderTokens.All(token => !normalized.Contains(token, StringComparison.Ordinal));
    }

    private static string NormalizeVendorKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.ToLowerInvariant();
        foreach (string token in VendorNoiseTokens)
        {
            normalized = normalized.Replace(token, string.Empty, StringComparison.Ordinal);
        }

        normalized = new string(normalized.Where(char.IsLetterOrDigit).ToArray());
        return VendorAliases.TryGetValue(normalized, out string? alias)
            ? alias
            : normalized;
    }

    private sealed record FirmwareSupportRoute(
        string DisplayName,
        string? SupportUrl,
        string RouteLabel);

    private static readonly Dictionary<string, FirmwareSupportRoute> SupportRoutes = new(StringComparer.Ordinal)
    {
        ["asus"] = new("ASUS", "https://www.asus.com/support/", "Official ASUS firmware route"),
        ["msi"] = new("MSI", "https://www.msi.com/support", "Official MSI firmware route"),
        ["gigabyte"] = new("Gigabyte", "https://www.gigabyte.com/Support", "Official Gigabyte firmware route"),
        ["asrock"] = new("ASRock", "https://www.asrock.com/support/", "Official ASRock firmware route"),
        ["biostar"] = new("BIOSTAR", "https://www.biostar.com.tw/app/en/support/", "Official BIOSTAR firmware route"),
        ["dell"] = new("Dell", "https://www.dell.com/support/home", "Official Dell firmware route"),
        ["hp"] = new("HP", "https://support.hp.com/", "Official HP firmware route"),
        ["lenovo"] = new("Lenovo", "https://pcsupport.lenovo.com/", "Official Lenovo firmware route"),
        ["acer"] = new("Acer", "https://www.acer.com/support", "Official Acer firmware route"),
        ["supermicro"] = new("Supermicro", "https://www.supermicro.com/en/support/resources", "Official Supermicro firmware route"),
        ["intel"] = new("Intel", "https://www.intel.com/content/www/us/en/support.html", "Official Intel firmware route"),
        ["framework"] = new("Framework", "https://knowledgebase.frame.work/", "Official Framework firmware route"),
        ["samsung"] = new("Samsung", "https://www.samsung.com/support/", "Official Samsung firmware route"),
        ["fujitsu"] = new("Fujitsu", "https://support.ts.fujitsu.com/", "Official Fujitsu firmware route")
    };

    private static readonly string[] PlaceholderTokens =
    [
        "system manufacturer",
        "system product name",
        "system version",
        "default string",
        "to be filled by o.e.m",
        "to be filled by oem",
        "base board product name",
        "not applicable",
        "unknown"
    ];

    private static readonly string[] VendorNoiseTokens =
    [
        "corporation",
        "computer",
        "computers",
        "company",
        "technology",
        "technologies",
        "electronics",
        "systems",
        "corp",
        "co",
        "inc",
        "ltd",
        "limited",
        "llc"
    ];

    private static readonly Dictionary<string, string> VendorAliases = new(StringComparer.Ordinal)
    {
        ["asustek"] = "asus",
        ["asustekcomputer"] = "asus",
        ["asustekcomputerinc"] = "asus",
        ["microstarinternational"] = "msi",
        ["microstarinternationalco"] = "msi",
        ["microstarinternationalcoltd"] = "msi",
        ["hewlettpackard"] = "hp",
        ["hpinc"] = "hp",
        ["dellinc"] = "dell",
        ["lenovogroup"] = "lenovo",
        ["gigabytetechnology"] = "gigabyte",
        ["gigabytetechnologyco"] = "gigabyte",
        ["gigabytetechnologycoltd"] = "gigabyte",
        ["biostarmicrotech"] = "biostar",
        ["supermicrocomputer"] = "supermicro",
        ["frameworkcomputer"] = "framework"
    };
}
