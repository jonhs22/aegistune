using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AegisTune.SystemIntegration;

internal static partial class HpSupportCatalogResolver
{
    public static HpProductSearchMatch? ResolveBestProductMatch(string typeaheadJson, string supportModel)
    {
        if (string.IsNullOrWhiteSpace(typeaheadJson) || string.IsNullOrWhiteSpace(supportModel))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(typeaheadJson);
        if (!document.RootElement.TryGetProperty("matches", out JsonElement matches)
            || matches.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string supportKey = NormalizeModelKey(supportModel);
        HashSet<string> supportTokens = ExtractModelTokens(supportModel);
        HashSet<string> supportProductCodes = ExtractProductCodes(supportModel);

        HpProductSearchCandidate? bestCandidate = matches
            .EnumerateArray()
            .Select(item => TryCreateCandidate(item, supportKey, supportTokens, supportProductCodes))
            .Where(candidate => candidate is not null)
            .Cast<HpProductSearchCandidate>()
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.IsBto)
            .ThenBy(candidate => candidate.ExtraTokenCount)
            .ThenBy(candidate => candidate.Match.DisplayTitle.Length)
            .FirstOrDefault(candidate => candidate.Score >= 260);

        return bestCandidate?.Match;
    }

    public static HpOsSelection? ResolvePreferredWindowsSelection(string osVersionJson)
    {
        if (string.IsNullOrWhiteSpace(osVersionJson))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(osVersionJson);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("osversions", out JsonElement osVersions)
            || osVersions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<HpOsSelection> candidates = new();

        foreach (JsonElement section in osVersions.EnumerateArray())
        {
            string osName = GetString(section, "name");
            if (!osName.StartsWith("Windows", StringComparison.OrdinalIgnoreCase)
                || !section.TryGetProperty("osVersionList", out JsonElement osVersionList)
                || osVersionList.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement version in osVersionList.EnumerateArray())
            {
                string osVersionName = GetString(version, "name");
                string osTmsId = GetString(version, "id");
                if (string.IsNullOrWhiteSpace(osVersionName) || string.IsNullOrWhiteSpace(osTmsId))
                {
                    continue;
                }

                candidates.Add(new HpOsSelection(osName, osVersionName, osTmsId));
            }
        }

        return candidates
            .OrderByDescending(GetOsSelectionScore)
            .ThenByDescending(candidate => BuildWindowsVersionSortKey(candidate.OsVersionName))
            .FirstOrDefault();
    }

    public static HpBiosReleaseMatch? ResolveLatestBios(string driverDetailsJson)
    {
        if (string.IsNullOrWhiteSpace(driverDetailsJson))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(driverDetailsJson);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("softwareTypes", out JsonElement softwareTypes)
            || softwareTypes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        HpBiosReleaseMatch[] matches = softwareTypes
            .EnumerateArray()
            .Where(type => IsBiosSoftwareType(type))
            .SelectMany(type => EnumerateBiosMatches(type))
            .GroupBy(match => match.SoftwareItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(match => match.ReleaseDate ?? DateTimeOffset.MinValue)
            .ThenByDescending(match => BuildVersionSortKey(match.Version))
            .ToArray();

        return matches.FirstOrDefault();
    }

    public static string BuildDriversPageUrl(string seoFriendlyName, long productId) =>
        $"https://support.hp.com/us-en/drivers/{seoFriendlyName}/{productId.ToString(CultureInfo.InvariantCulture)}";

    private static IEnumerable<HpBiosReleaseMatch> EnumerateBiosMatches(JsonElement softwareType)
    {
        if (!softwareType.TryGetProperty("softwareDriversList", out JsonElement softwareDriversList)
            || softwareDriversList.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement entry in softwareDriversList.EnumerateArray())
        {
            HpBiosReleaseMatch? match = TryCreateBiosMatch(entry);
            if (match is not null)
            {
                yield return match;
            }
        }
    }

    private static HpBiosReleaseMatch? TryCreateBiosMatch(JsonElement entry)
    {
        if (!entry.TryGetProperty("latestVersionDriver", out JsonElement driver)
            || driver.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string title = GetString(driver, "title");
        string description = GetString(driver, "detailInformation", "description");
        if (!IsBiosDriver(title, description))
        {
            return null;
        }

        string version = GetString(driver, "version");
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        DateTimeOffset? releaseDate = ParseDate(GetString(driver, "detailInformation", "releaseDate"))
            ?? ParseDate(GetString(driver, "releaseDate"));
        string detailsUrl = EnumerateAssociatedContent(entry)
            .Select(content => GetString(content, "diskAttachmentLink"))
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
            ?? GetString(driver, "url");
        string packageUrl = GetString(driver, "fileUrl");
        string softwareItemId = GetString(driver, "softwareItemId");

        return new HpBiosReleaseMatch(
            softwareItemId,
            title,
            version,
            releaseDate,
            string.IsNullOrWhiteSpace(detailsUrl) ? null : detailsUrl,
            string.IsNullOrWhiteSpace(packageUrl) ? null : packageUrl,
            description);
    }

    private static IEnumerable<JsonElement> EnumerateAssociatedContent(JsonElement entry)
    {
        if (entry.TryGetProperty("associatedContentList", out JsonElement associatedContent)
            && associatedContent.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement content in associatedContent.EnumerateArray())
            {
                yield return content;
            }
        }
    }

    private static bool IsBiosSoftwareType(JsonElement softwareType)
    {
        string accordionName = GetString(softwareType, "accordionName");
        string tmsName = GetString(softwareType, "tmsName");
        return accordionName.Contains("BIOS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tmsName, "BIOS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBiosDriver(string title, string description) =>
        title.Contains("BIOS", StringComparison.OrdinalIgnoreCase)
        || (title.Contains("Firmware", StringComparison.OrdinalIgnoreCase)
            && description.Contains("System BIOS", StringComparison.OrdinalIgnoreCase));

    private static HpProductSearchCandidate? TryCreateCandidate(
        JsonElement item,
        string supportKey,
        HashSet<string> supportTokens,
        HashSet<string> supportProductCodes)
    {
        if (!string.Equals(GetString(item, "activeWebSupportFlag"), "yes", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string displayTitle = GetString(item, "name");
        string seoFriendlyName = GetString(item, "seoFriendlyName");
        long? productId = GetInt64(item, "productId");
        long? seriesOid = GetInt64(item, "pmSeriesOid") ?? productId;
        if (string.IsNullOrWhiteSpace(displayTitle)
            || string.IsNullOrWhiteSpace(seoFriendlyName)
            || productId is null
            || seriesOid is null)
        {
            return null;
        }

        string pmClass = GetString(item, "pmClass");
        bool isBto = GetBoolean(item, "btoFlag");
        HashSet<string> candidateTokens = ExtractModelTokens(displayTitle);
        HashSet<string> candidateProductCodes = ExtractProductCodes(displayTitle);
        string candidateKey = NormalizeModelKey(displayTitle);
        int overlappingTokens = candidateTokens.Intersect(supportTokens, StringComparer.OrdinalIgnoreCase).Count();
        int extraTokenCount = candidateTokens.Except(supportTokens, StringComparer.OrdinalIgnoreCase).Count();

        if (supportTokens.Count > 0 && overlappingTokens == 0)
        {
            return null;
        }

        int score = 0;
        if (string.Equals(candidateKey, supportKey, StringComparison.OrdinalIgnoreCase))
        {
            score += 600;
        }

        score += overlappingTokens * 30;
        if (supportTokens.Count > 0 && overlappingTokens == supportTokens.Count)
        {
            score += 120;
        }

        score -= extraTokenCount * 10;
        score += string.Equals(pmClass, "pm_series_value", StringComparison.OrdinalIgnoreCase) ? 140 : -60;

        if (ContainsRefurbished(displayTitle) && !ContainsRefurbishedFromSupport(supportTokens))
        {
            score -= 220;
        }

        if (supportProductCodes.Count > 0)
        {
            int overlappingCodes = candidateProductCodes.Intersect(supportProductCodes, StringComparer.OrdinalIgnoreCase).Count();
            score += overlappingCodes * 180;
            if (overlappingCodes == 0 && string.Equals(pmClass, "pm_name_value", StringComparison.OrdinalIgnoreCase))
            {
                score -= 120;
            }
        }
        else if (isBto)
        {
            score -= 140;
        }

        HpProductSearchMatch match = new(
            displayTitle,
            productId.Value,
            seriesOid.Value,
            seoFriendlyName,
            BuildDriversPageUrl(seoFriendlyName, productId.Value));

        return new HpProductSearchCandidate(match, score, extraTokenCount, isBto);
    }

    private static int GetOsSelectionScore(HpOsSelection selection)
    {
        if (string.Equals(selection.OsName, "Windows 11", StringComparison.OrdinalIgnoreCase)
            && string.Equals(selection.OsVersionName, "Windows 11", StringComparison.OrdinalIgnoreCase))
        {
            return 500;
        }

        if (string.Equals(selection.OsName, "Windows 11", StringComparison.OrdinalIgnoreCase))
        {
            return 420;
        }

        if (string.Equals(selection.OsName, "Windows 10", StringComparison.OrdinalIgnoreCase)
            && string.Equals(selection.OsVersionName, "Windows 10 (64-bit)", StringComparison.OrdinalIgnoreCase))
        {
            return 320;
        }

        if (string.Equals(selection.OsName, "Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            return 260;
        }

        return 100;
    }

    private static string BuildWindowsVersionSortKey(string versionName)
    {
        Match match = WindowsVersionRegex().Match(versionName);
        if (!match.Success)
        {
            return versionName.ToUpperInvariant();
        }

        string major = match.Groups["major"].Value;
        string year = match.Groups["year"].Success ? match.Groups["year"].Value : "00";
        string half = match.Groups["half"].Success ? match.Groups["half"].Value : "0";
        return $"{major.PadLeft(2, '0')}.{year.PadLeft(2, '0')}.{half.PadLeft(2, '0')}.{versionName.ToUpperInvariant()}";
    }

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset parsed)
            ? parsed
            : null;

    private static string BuildVersionSortKey(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0000000000";
        }

        string[] parts = version
            .Split(['.', '-', '_', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IEnumerable<string> normalizedParts = parts.Select(part =>
            int.TryParse(part.TrimStart('V', 'v'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric)
                ? numeric.ToString("D10", CultureInfo.InvariantCulture)
                : part.ToUpperInvariant());

        return string.Join(".", normalizedParts);
    }

    private static bool ContainsRefurbished(string value) =>
        value.Contains("refurbished", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsRefurbishedFromSupport(HashSet<string> supportTokens) =>
        supportTokens.Contains("refurbished") || supportTokens.Contains("certified");

    private static HashSet<string> ExtractModelTokens(string value)
    {
        HashSet<string> tokens = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ModelTokenRegex().Matches(value))
        {
            string token = match.Value.ToLowerInvariant();
            if (IgnoredModelTokens.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static HashSet<string> ExtractProductCodes(string value)
    {
        HashSet<string> codes = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ProductCodeRegex().Matches(value))
        {
            string code = match.Value.ToUpperInvariant();
            if (IgnoredProductCodes.Contains(code))
            {
                continue;
            }

            codes.Add(code);
        }

        return codes;
    }

    private static string GetString(JsonElement element, params string[] path) =>
        TryNavigate(element, path, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long? GetInt64(JsonElement element, params string[] path)
    {
        if (!TryNavigate(element, path, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long numeric) => numeric,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) => parsed,
            _ => null
        };
    }

    private static bool GetBoolean(JsonElement element, params string[] path)
    {
        if (!TryNavigate(element, path, out JsonElement value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out bool parsed) => parsed,
            _ => false
        };
    }

    private static bool TryNavigate(JsonElement element, string[] path, out JsonElement value)
    {
        value = element;
        foreach (string segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeModelKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    [GeneratedRegex(@"Windows\s+(?<major>10|11)(?:\s+version\s+(?<year>\d{2})H(?<half>[12]))?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WindowsVersionRegex();

    [GeneratedRegex("[A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex ModelTokenRegex();

    [GeneratedRegex(@"\b[A-Z0-9]{6,7}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ProductCodeRegex();

    private static readonly HashSet<string> IgnoredModelTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "hp",
        "pc",
        "pcs",
        "notebook",
        "laptop",
        "desktop",
        "computer",
        "series",
        "inch",
        "model"
    };

    private static readonly HashSet<string> IgnoredProductCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "64BIT",
        "32BIT",
        "24H2",
        "25H2",
        "23H2",
        "22H2",
        "21H2"
    };
}

internal sealed record HpProductSearchMatch(
    string DisplayTitle,
    long ProductId,
    long SeriesOid,
    string SeoFriendlyName,
    string DriversPageUrl);

internal sealed record HpOsSelection(
    string OsName,
    string OsVersionName,
    string OsTmsId);

internal sealed record HpBiosReleaseMatch(
    string SoftwareItemId,
    string Title,
    string Version,
    DateTimeOffset? ReleaseDate,
    string? DetailsUrl,
    string? PackageUrl,
    string Description);

internal sealed record HpProductSearchCandidate(
    HpProductSearchMatch Match,
    int Score,
    int ExtraTokenCount,
    bool IsBto);
