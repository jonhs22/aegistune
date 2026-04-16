using System.Globalization;
using System.Xml.Linq;

namespace AegisTune.SystemIntegration;

internal static class DellCatalogFirmwareReleaseResolver
{
    public static DellCatalogFirmwareReleaseMatch? ResolveLatestBios(
        string catalogXml,
        string supportModel)
    {
        if (string.IsNullOrWhiteSpace(catalogXml) || string.IsNullOrWhiteSpace(supportModel))
        {
            return null;
        }

        string supportKey = NormalizeModelKey(supportModel);
        if (string.IsNullOrWhiteSpace(supportKey))
        {
            return null;
        }

        XDocument document = XDocument.Parse(catalogXml, LoadOptions.None);

        DellCatalogFirmwareReleaseMatch[] matches = document
            .Descendants("SoftwareComponent")
            .Where(IsBiosComponent)
            .Select(component => TryCreateMatch(component, supportKey))
            .Where(match => match is not null)
            .Cast<DellCatalogFirmwareReleaseMatch>()
            .GroupBy(match => match.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(match => match.ReleaseDate ?? DateTimeOffset.MinValue)
            .ThenByDescending(match => BuildVersionSortKey(match.Version))
            .ToArray();

        return matches.FirstOrDefault();
    }

    private static DellCatalogFirmwareReleaseMatch? TryCreateMatch(XElement component, string supportKey)
    {
        XElement[] matchingModels = component
            .Descendants("Brand")
            .SelectMany(brand =>
                brand.Elements("Model")
                    .Where(model => IsExactSupportedModelMatch(
                        supportKey,
                        GetDisplayValue(brand.Element("Display")),
                        GetDisplayValue(model.Element("Display")))))
            .ToArray();

        if (matchingModels.Length == 0)
        {
            return null;
        }

        string version = component.Attribute("vendorVersion")?.Value
            ?? component.Attribute("dellVersion")?.Value
            ?? "Unknown";

        return new DellCatalogFirmwareReleaseMatch(
            component.Attribute("packageID")?.Value ?? Guid.NewGuid().ToString("N"),
            version,
            ParseReleaseDate(component),
            NormalizeDellUrl(component.Element("ImportantInfo")?.Attribute("URL")?.Value),
            component.Attribute("path")?.Value,
            GetDisplayValue(component.Element("Name")?.Element("Display")) ?? "Dell BIOS package",
            matchingModels
                .Select(model => GetDisplayValue(model.Element("Display")))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            matchingModels
                .Select(model => model.Attribute("systemID")?.Value)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool IsBiosComponent(XElement component) =>
        string.Equals(
            component.Element("ComponentType")?.Attribute("value")?.Value,
            "BIOS",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsExactSupportedModelMatch(string supportKey, string? brandLabel, string? modelLabel)
    {
        HashSet<string> candidateKeys = new(StringComparer.OrdinalIgnoreCase);

        AddKey(candidateKeys, modelLabel);
        AddKey(candidateKeys, $"{brandLabel} {modelLabel}");

        return candidateKeys.Contains(supportKey);
    }

    private static void AddKey(HashSet<string> keys, string? value)
    {
        string key = NormalizeModelKey(value);
        if (!string.IsNullOrWhiteSpace(key))
        {
            keys.Add(key);
        }
    }

    private static string NormalizeModelKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(
            value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private static string? GetDisplayValue(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return string.Concat(element.Nodes().OfType<XCData>().Select(node => node.Value)).Trim() switch
        {
            "" => element.Value.Trim(),
            string cdata => cdata
        };
    }

    private static DateTimeOffset? ParseReleaseDate(XElement component)
    {
        string? releaseDate = component.Attribute("releaseDate")?.Value;
        if (!string.IsNullOrWhiteSpace(releaseDate)
            && DateTimeOffset.TryParse(
                releaseDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset parsedReleaseDate))
        {
            return parsedReleaseDate;
        }

        string? dateTime = component.Attribute("dateTime")?.Value;
        if (!string.IsNullOrWhiteSpace(dateTime)
            && DateTimeOffset.TryParse(
                dateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset parsedDateTime))
        {
            return parsedDateTime;
        }

        return null;
    }

    private static string NormalizeDellUrl(string? url) =>
        string.IsNullOrWhiteSpace(url)
            ? "https://www.dell.com/support/home/en-us"
            : url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);

    private static string BuildVersionSortKey(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0000000000";
        }

        string[] parts = version
            .Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IEnumerable<string> normalizedParts = parts.Select(part =>
            int.TryParse(part.TrimStart('A', 'a', 'V', 'v'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric)
                ? numeric.ToString("D10", CultureInfo.InvariantCulture)
                : part.ToUpperInvariant());

        return string.Join(".", normalizedParts);
    }
}

internal sealed record DellCatalogFirmwareReleaseMatch(
    string PackageId,
    string Version,
    DateTimeOffset? ReleaseDate,
    string DetailsUrl,
    string? PackagePath,
    string PackageName,
    IReadOnlyList<string> MatchedModelLabels,
    IReadOnlyList<string> MatchedSystemIds);
