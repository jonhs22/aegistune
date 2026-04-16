using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AegisTune.SystemIntegration;

internal static partial class LenovoSupportCatalogResolver
{
    public static LenovoProductSearchMatch? ResolveBestProductMatch(string searchJson, string supportModel)
    {
        if (string.IsNullOrWhiteSpace(searchJson) || string.IsNullOrWhiteSpace(supportModel))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(searchJson);
        if (!document.RootElement.TryGetProperty("Results", out JsonElement results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        HashSet<string> supportTokens = ExtractModelTokens(supportModel);
        HashSet<string> supportTypeCodes = ExtractTypeCodes(supportModel);
        string supportKey = NormalizeModelKey(supportModel);

        LenovoProductSearchCandidate? bestCandidate = results
            .EnumerateArray()
            .Where(item => string.Equals(GetString(item, "Type"), "Product", StringComparison.OrdinalIgnoreCase))
            .Select(item => TryCreateCandidate(item, supportKey, supportTokens, supportTypeCodes))
            .Where(candidate => candidate is not null)
            .Cast<LenovoProductSearchCandidate>()
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.PathDepth)
            .ThenBy(candidate => candidate.ExtraTypeCount)
            .ThenBy(candidate => candidate.Match.DisplayTitle.Length)
            .FirstOrDefault();

        return bestCandidate?.Match;
    }

    public static LenovoBiosReleaseMatch? ResolveLatestBios(string downloadsJson)
    {
        if (string.IsNullOrWhiteSpace(downloadsJson))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(downloadsJson);
        if (!document.RootElement.TryGetProperty("body", out JsonElement body)
            || !body.TryGetProperty("DownloadItems", out JsonElement downloadItems)
            || downloadItems.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        LenovoBiosReleaseMatch[] matches = downloadItems
            .EnumerateArray()
            .Select(TryCreateBiosMatch)
            .Where(match => match is not null)
            .Cast<LenovoBiosReleaseMatch>()
            .GroupBy(match => match.DocId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(match => match.ReleaseDate ?? DateTimeOffset.MinValue)
            .ThenByDescending(match => BuildVersionSortKey(match.Version))
            .ToArray();

        return matches.FirstOrDefault();
    }

    public static string BuildDriversPageUrl(string productId) =>
        $"https://pcsupport.lenovo.com/us/en/products/{productId.ToLowerInvariant()}/downloads/driver-list";

    private static LenovoProductSearchCandidate? TryCreateCandidate(
        JsonElement item,
        string supportKey,
        HashSet<string> supportTokens,
        HashSet<string> supportTypeCodes)
    {
        string productId = GetString(item, "ID");
        if (string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        string displayTitle = StripMarkup(GetString(item, "Title"));
        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            return null;
        }

        HashSet<string> candidateTokens = ExtractModelTokens($"{displayTitle} {productId}");
        HashSet<string> candidateTypeCodes = ExtractTypeCodes($"{displayTitle} {productId}");
        string titleKey = NormalizeModelKey(displayTitle);
        string productKey = NormalizeModelKey(productId);
        int score = 0;

        if (supportTypeCodes.Count > 0)
        {
            int overlappingTypes = candidateTypeCodes.Intersect(supportTypeCodes, StringComparer.OrdinalIgnoreCase).Count();
            if (overlappingTypes == 0)
            {
                return null;
            }

            score += overlappingTypes * 100;
            score += candidateTypeCodes.SetEquals(supportTypeCodes)
                ? 300
                : supportTypeCodes.All(typeCode => candidateTypeCodes.Contains(typeCode))
                    ? 220
                    : 0;

            if (supportTypeCodes.Count == 1
                && supportTypeCodes.Any(typeCode =>
                    productId.EndsWith($"/{typeCode}", StringComparison.OrdinalIgnoreCase)
                    || displayTitle.Contains($"Type {typeCode}", StringComparison.OrdinalIgnoreCase)))
            {
                score += 140;
            }

            score -= Math.Max(0, candidateTypeCodes.Count - supportTypeCodes.Count) * 15;
        }

        int overlappingTokens = candidateTokens.Intersect(supportTokens, StringComparer.OrdinalIgnoreCase).Count();
        if (supportTokens.Count > 0 && overlappingTokens == 0 && supportTypeCodes.Count == 0)
        {
            return null;
        }

        score += overlappingTokens * 25;
        if (supportTokens.Count > 0 && overlappingTokens == supportTokens.Count)
        {
            score += 75;
        }

        if (string.Equals(titleKey, supportKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(productKey, supportKey, StringComparison.OrdinalIgnoreCase))
        {
            score += 300;
        }

        if (score <= 0)
        {
            return null;
        }

        LenovoProductSearchMatch match = new(
            productId,
            displayTitle,
            BuildDriversPageUrl(productId),
            candidateTypeCodes.ToArray());

        return new LenovoProductSearchCandidate(
            match,
            score,
            productId.Count(character => character == '/'),
            Math.Max(0, candidateTypeCodes.Count - supportTypeCodes.Count));
    }

    private static LenovoBiosReleaseMatch? TryCreateBiosMatch(JsonElement item)
    {
        if (!string.Equals(GetString(item, "Category", "Name"), "BIOS/UEFI", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string title = StripMarkup(GetString(item, "Title"));
        string summary = StripMarkup(GetString(item, "Summary"));
        if (!IsBiosUpdateItem(title, summary, item))
        {
            return null;
        }

        string version = GetString(item, "SummaryInfo", "Version");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = EnumerateFiles(item)
                .Select(file => GetString(file, "Version"))
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        string detailsUrl = EnumerateFiles(item)
            .Select(file => GetString(file, "URL"))
            .FirstOrDefault(url => url.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || url.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                || url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        string packageUrl = EnumerateFiles(item)
            .Select(file => GetString(file, "URL"))
            .FirstOrDefault(url => url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || url.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
                || url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        return new LenovoBiosReleaseMatch(
            GetString(item, "DocId"),
            title,
            version,
            GetUnixDate(item, "Date", "Unix"),
            string.IsNullOrWhiteSpace(detailsUrl) ? null : detailsUrl,
            string.IsNullOrWhiteSpace(packageUrl) ? null : packageUrl,
            GetString(item, "SummaryInfo", "Priority"),
            summary);
    }

    private static bool IsBiosUpdateItem(string title, string summary, JsonElement item)
    {
        if (title.Contains("BIOS Update", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (summary.Contains("updates the UEFI BIOS", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("updates the BIOS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return EnumerateFiles(item)
            .Select(file => GetString(file, "Name"))
            .Any(name => name.Contains("BIOS Update", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<JsonElement> EnumerateFiles(JsonElement item)
    {
        if (item.TryGetProperty("Files", out JsonElement files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement file in files.EnumerateArray())
            {
                yield return file;
            }
        }
    }

    private static DateTimeOffset? GetUnixDate(JsonElement element, params string[] path)
    {
        if (!TryNavigate(element, path, out JsonElement value))
        {
            return null;
        }

        long? milliseconds = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long numeric) => numeric,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) => parsed,
            _ => null
        };

        return milliseconds is null
            ? null
            : DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value);
    }

    private static string GetString(JsonElement element, params string[] path) =>
        TryNavigate(element, path, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

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

    private static string StripMarkup(string value) =>
        WebUtility.HtmlDecode(MarkupRegex().Replace(value, string.Empty)).Trim();

    private static HashSet<string> ExtractModelTokens(string value)
    {
        HashSet<string> tokens = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ModelTokenRegex().Matches(value))
        {
            string token = match.Value.ToLowerInvariant();
            if (token.Length == 1 && !char.IsDigit(token[0]))
            {
                continue;
            }

            if (IgnoredModelTokens.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static HashSet<string> ExtractTypeCodes(string value)
    {
        HashSet<string> typeCodes = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TypeCodeRegex().Matches(value))
        {
            typeCodes.Add(match.Value.ToUpperInvariant());
        }

        return typeCodes;
    }

    private static string NormalizeModelKey(string value) =>
        new(
            value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

    private static string BuildVersionSortKey(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0000000000";
        }

        string[] parts = version
            .Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IEnumerable<string> normalizedParts = parts.Select(part =>
            int.TryParse(part.TrimStart('V', 'v'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric)
                ? numeric.ToString("D10", CultureInfo.InvariantCulture)
                : part.ToUpperInvariant());

        return string.Join(".", normalizedParts);
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex MarkupRegex();

    [GeneratedRegex("[A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex ModelTokenRegex();

    [GeneratedRegex(@"\b[A-Z0-9]{4}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TypeCodeRegex();

    private static readonly HashSet<string> IgnoredModelTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "lenovo",
        "laptop",
        "laptops",
        "desktop",
        "desktops",
        "notebook",
        "notebooks",
        "type"
    };
}

internal sealed record LenovoProductSearchMatch(
    string ProductId,
    string DisplayTitle,
    string DriversPageUrl,
    IReadOnlyList<string> TypeCodes);

internal sealed record LenovoBiosReleaseMatch(
    string DocId,
    string Title,
    string Version,
    DateTimeOffset? ReleaseDate,
    string? DetailsUrl,
    string? PackageUrl,
    string Priority,
    string Summary);

internal sealed record LenovoProductSearchCandidate(
    LenovoProductSearchMatch Match,
    int Score,
    int PathDepth,
    int ExtraTypeCount);
