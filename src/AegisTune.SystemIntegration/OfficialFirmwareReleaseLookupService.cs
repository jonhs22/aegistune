using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class OfficialFirmwareReleaseLookupService : IFirmwareReleaseLookupService
{
    private readonly HttpClient _httpClient;
    private readonly IDellCatalogSource _dellCatalogSource;

    public OfficialFirmwareReleaseLookupService(
        HttpClient? httpClient = null,
        IDellCatalogSource? dellCatalogSource = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _dellCatalogSource = dellCatalogSource ?? new DellCatalogCabSource(_httpClient);
    }

    public async Task<FirmwareReleaseLookupResult> LookupAsync(
        FirmwareInventorySnapshot firmware,
        CancellationToken cancellationToken = default)
    {
        string vendorKey = NormalizeVendorKey(firmware.SupportManufacturer);

        return vendorKey switch
        {
            "asus" => await LookupAsusAsync(firmware, cancellationToken),
            "dell" => await LookupDellAsync(firmware, cancellationToken),
            "lenovo" => await LookupLenovoAsync(firmware, cancellationToken),
            "hp" => await LookupHpAsync(firmware, cancellationToken),
            "msi" => BuildMsiWorkflow(firmware),
            "gigabyte" => BuildGigabyteWorkflow(firmware),
            _ => BuildGenericWorkflow(firmware)
        };
    }

    private async Task<FirmwareReleaseLookupResult> LookupAsusAsync(
        FirmwareInventorySnapshot firmware,
        CancellationToken cancellationToken)
    {
        string[] candidateUrls =
        [
            $"https://www.asus.com/us/supportonly/{Uri.EscapeDataString(firmware.SupportModel)}/helpdesk_bios/",
            $"https://www.asus.com/supportonly/{Uri.EscapeDataString(firmware.SupportModel)}/helpdesk_bios/"
        ];

        Exception? lastProbeException = null;

        foreach (string candidateUrl in candidateUrls.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, candidateUrl);
                request.Headers.UserAgent.ParseAdd(DefaultUserAgent);

                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                string html = await response.Content.ReadAsStringAsync(cancellationToken);
                FirmwareReleaseProbe? probe = ParseAsusSupportPage(html);
                if (probe is null)
                {
                    continue;
                }

                string betaClause = probe.IsBeta
                    ? " ASUS marks the latest listed BIOS as a Beta release, so keep it on a technician-only review path."
                    : string.Empty;
                bool currentMatchesLatest = FirmwareVersionComparer.AreEquivalent(firmware.BiosVersionLabel, probe.Version);
                string comparisonSummary = currentMatchesLatest
                    ? $"Current BIOS {firmware.BiosVersionLabel} matches the latest version listed on the official ASUS support page.{betaClause}"
                    : $"Official ASUS support lists BIOS {probe.Version} dated {probe.ReleaseDate?.ToLocalTime().ToString("d") ?? "unknown date"}, while this machine reports {firmware.BiosVersionLabel}.{betaClause} Verify the board revision and release notes before flashing.";

                return new FirmwareReleaseLookupResult(
                    FirmwareReleaseLookupMode.DirectVendorPage,
                    "ASUS",
                    firmware.SupportIdentityLabel,
                    firmware.BiosVersionLabel,
                    firmware.BiosReleaseDate,
                    $"Latest BIOS information was verified from the official ASUS support page for {firmware.SupportModel}.",
                    probe.IsBeta
                        ? "The official ASUS page marks the latest BIOS listing as Beta. Use the official ASUS BIOS page, review release notes, and keep rollback and power-safety gates explicit before any flash."
                        : "Use the official ASUS BIOS page and compare release notes against the exact board revision before any flash. Keep rollback and power-safety gates explicit.",
                    comparisonSummary,
                    firmware.SupportSearchHint,
                    candidateUrl,
                    candidateUrl,
                    probe.Version,
                    probe.ReleaseDate,
                    "MyASUS",
                    "MyASUS can assist with ASUS support workflows, but the BIOS decision should still stay on the official board support page.",
                    "Official ASUS support page",
                    probe.IsBeta ? "The latest ASUS BIOS listing is marked Beta on the official support page." : null,
                    DateTimeOffset.Now,
                    probe.IsBeta,
                    $"BIOS {probe.Version}");
            }
            catch (Exception ex)
            {
                lastProbeException = ex;
            }
        }

        if (lastProbeException is not null)
        {
            return new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.LookupFailed,
                "ASUS",
                firmware.SupportIdentityLabel,
                firmware.BiosVersionLabel,
                firmware.BiosReleaseDate,
                "The ASUS latest-BIOS probe failed during the live vendor check.",
                "Open the official ASUS support route manually and verify the model one more time before comparing BIOS versions.",
                "No automatic latest-version comparison was completed.",
                firmware.SupportSearchHint,
                firmware.PrimarySupportUrl,
                firmware.PrimarySupportUrl,
                null,
                null,
                "MyASUS",
                "Use MyASUS only as a convenience layer. The authoritative BIOS decision still belongs to the official ASUS support page for the exact board.",
                "Official ASUS support page probe",
                lastProbeException.Message,
                DateTimeOffset.Now);
        }

        return new FirmwareReleaseLookupResult(
            FirmwareReleaseLookupMode.VendorSupportSearch,
            "ASUS",
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "A direct ASUS BIOS listing was not verified automatically, but the official ASUS support route is ready.",
            "Open the official ASUS support page and confirm the exact model and board revision before comparing BIOS packages.",
            "No automatic latest-version comparison was completed. Use the official ASUS route and compare the listed BIOS against the current machine.",
            firmware.SupportSearchHint,
            firmware.PrimarySupportUrl,
            firmware.PrimarySupportUrl,
            null,
            null,
            "MyASUS",
            "If needed, MyASUS can assist with support flows, but keep BIOS validation on the official ASUS support page.",
            "Official ASUS support route",
            null,
            DateTimeOffset.Now);
    }

    private async Task<FirmwareReleaseLookupResult> LookupDellAsync(
        FirmwareInventorySnapshot firmware,
        CancellationToken cancellationToken)
    {
        try
        {
            string? catalogXml = await _dellCatalogSource.GetCatalogXmlAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(catalogXml))
            {
                return BuildDellCatalogWorkflow(firmware, "The official Dell client catalog did not return a readable payload for this lookup.");
            }

            DellCatalogFirmwareReleaseMatch? match = DellCatalogFirmwareReleaseResolver.ResolveLatestBios(catalogXml, firmware.SupportModel);
            if (match is null)
            {
                return BuildDellCatalogWorkflow(
                    firmware,
                    $"No exact Dell BIOS catalog match was resolved for {firmware.SupportModel}. Keep the workflow on Dell Support or Dell Command | Update.");
            }

            string matchedModels = match.MatchedModelLabels.Count == 0
                ? firmware.SupportModel
                : string.Join(", ", match.MatchedModelLabels);
            bool currentMatchesLatest = FirmwareVersionComparer.AreEquivalent(firmware.BiosVersionLabel, match.Version);
            string comparisonSummary = currentMatchesLatest
                ? $"Current BIOS {firmware.BiosVersionLabel} matches the latest version listed in the official Dell client catalog for {matchedModels}."
                : $"Official Dell client catalog lists BIOS {match.Version} dated {match.ReleaseDate?.ToLocalTime().ToString("d") ?? "unknown date"} for {matchedModels}, while this machine reports {firmware.BiosVersionLabel}. Verify the Service Tag or exact product identity in Dell Support before flashing.";

            return new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.CatalogFeed,
                "Dell",
                firmware.SupportIdentityLabel,
                firmware.BiosVersionLabel,
                firmware.BiosReleaseDate,
                $"Latest BIOS information was verified from the official Dell client catalog for {matchedModels}.",
                "Use Dell Support or Dell Command | Update with the exact product or Service Tag, review the Dell release details, and keep rollback plus power-safety gates explicit before any flash.",
                comparisonSummary,
                $"Use Dell Support or Dell Command | Update to confirm {matchedModels} against the current machine identity before applying BIOS {match.Version}.",
                "https://www.dell.com/support/home/en-us",
                match.DetailsUrl,
                match.Version,
                match.ReleaseDate,
                "Dell Command | Update",
                "The official Dell client catalog is a valid trust source for supported systems, but the final flash decision should still be confirmed through Dell Support or Dell Command | Update on the exact endpoint.",
                "Official Dell client catalog (CatalogPC.cab)",
                null,
                DateTimeOffset.Now,
                false,
                match.PackageName);
        }
        catch (Exception ex)
        {
            return BuildDellCatalogWorkflow(
                firmware,
                $"The official Dell catalog lookup failed: {ex.Message}");
        }
    }

    private static FirmwareReleaseLookupResult BuildMsiWorkflow(FirmwareInventorySnapshot firmware) =>
        new(
            FirmwareReleaseLookupMode.VendorSupportSearch,
            "MSI",
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "Official MSI support is ready, but the app did not claim a latest BIOS version automatically.",
            "Use the official MSI download center, search the exact model, and verify the BIOS page manually before any flash.",
            "Latest BIOS version is not auto-verified for MSI yet. Keep this on an official MSI support search path.",
            $"Search the MSI download center for \"{firmware.SupportModel}\" and open the exact product support page before comparing BIOS releases.",
            "https://www.msi.com/support/download/driver-1",
            "https://www.msi.com/support/download/driver-1",
            null,
            null,
            "MSI Download Center",
            "Search the exact consumer or business model inside the official MSI support flow. Do not guess a BIOS page URL without a verified product slug.",
            "Official MSI support route",
            null,
            DateTimeOffset.Now);

    private static FirmwareReleaseLookupResult BuildGigabyteWorkflow(FirmwareInventorySnapshot firmware) =>
        new(
            FirmwareReleaseLookupMode.VendorSupportSearch,
            "Gigabyte",
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "Official Gigabyte support is ready, but the app did not claim a latest BIOS version automatically.",
            "Use the official Gigabyte support route and verify the exact board model and revision before comparing BIOS releases.",
            "Latest BIOS version is not auto-verified for Gigabyte yet. Keep this on an official board-support path.",
            $"Search the official Gigabyte support site for \"{firmware.SupportModel}\" and confirm the exact board revision before comparing BIOS files.",
            "https://www.gigabyte.com/Support",
            "https://www.gigabyte.com/Support",
            null,
            null,
            "Gigabyte Support",
            "Gigabyte product pages often depend on exact board revision. Treat revision confirmation as mandatory before any BIOS decision.",
            "Official Gigabyte support route",
            null,
            DateTimeOffset.Now);

    private static FirmwareReleaseLookupResult BuildDellCatalogWorkflow(
        FirmwareInventorySnapshot firmware,
        string? warningMessage = null) =>
        new(
            FirmwareReleaseLookupMode.CatalogFeed,
            "Dell",
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "Dell has an official commercial update-catalog path, but the app did not auto-resolve a latest BIOS release for this exact machine yet.",
            "Use Dell Support or Dell Command | Update with the exact product or Service Tag. For business systems, Dell's official catalogs are the safer automation route than page scraping.",
            "Latest BIOS version is not auto-verified here. The vendor workflow is official and catalog-backed, but the exact Dell product identity still needs confirmation.",
            $"Open Dell Support and identify the exact machine with Service Tag or product identity before comparing BIOS updates for {firmware.SupportModel}.",
            "https://www.dell.com/support/home/en-us",
            "https://www.dell.com/support/home/en-us?app=drivers",
            null,
            null,
            "Dell Command | Update",
            "For commercial systems, Dell Command | Update and Dell's driver-pack catalogs are the right BIOS workflow. Keep consumer page-scraping out of the trust path.",
            "Official Dell support route and Dell commercial catalogs",
            warningMessage,
            DateTimeOffset.Now);

    private async Task<FirmwareReleaseLookupResult> LookupHpAsync(
        FirmwareInventorySnapshot firmware,
        CancellationToken cancellationToken)
    {
        string supportSearchUrl = "https://support.hp.com/us-en/drivers";
        string typeaheadUrl = BuildHpTypeaheadUrl(firmware.SupportModel);

        try
        {
            string typeaheadJson = await GetResponseTextAsync(typeaheadUrl, supportSearchUrl, cancellationToken);
            HpProductSearchMatch? productMatch = HpSupportCatalogResolver.ResolveBestProductMatch(typeaheadJson, firmware.SupportModel);
            if (productMatch is null)
            {
                return BuildHpWorkflow(
                    firmware,
                    supportSearchUrl,
                    $"Official HP Support typeahead did not resolve an exact product-series match for {firmware.SupportModel}. Keep BIOS validation on HP Support or HP Image Assistant / HP CMSL.");
            }

            string osVersionUrl = $"https://support.hp.com/wcc-services/swd-v2/osVersionData?cc=us&lc=en&productOid={productMatch.ProductId.ToString(System.Globalization.CultureInfo.InvariantCulture)}&authState=anonymous&template=SWD-LaptopLanding";
            string osVersionJson = await GetResponseTextAsync(osVersionUrl, productMatch.DriversPageUrl, cancellationToken);
            HpOsSelection? osSelection = HpSupportCatalogResolver.ResolvePreferredWindowsSelection(osVersionJson);
            if (osSelection is null)
            {
                return BuildHpWorkflow(
                    firmware,
                    productMatch.DriversPageUrl,
                    $"Official HP Support resolved {productMatch.DisplayTitle}, but no deterministic Windows driver-feed selection was available for the BIOS comparison.");
            }

            string driverDetailsJson = await PostJsonAsync(
                "https://support.hp.com/wcc-services/swd-v2/driverDetails?authState=anonymous&template=SWDSeriesDownload_OSselect",
                productMatch.DriversPageUrl,
                new
                {
                    lc = "en",
                    cc = "us",
                    osTMSId = osSelection.OsTmsId,
                    osName = osSelection.OsName,
                    productSeriesOid = productMatch.SeriesOid,
                    platformId = osSelection.OsTmsId
                },
                cancellationToken);

            HpBiosReleaseMatch? biosMatch = HpSupportCatalogResolver.ResolveLatestBios(driverDetailsJson);
            if (biosMatch is null)
            {
                return BuildHpWorkflow(
                    firmware,
                    productMatch.DriversPageUrl,
                    $"Official HP Support resolved {productMatch.DisplayTitle}, but no deterministic BIOS package was auto-selected from the HP driver feed.");
            }

            string comparisonSummary = FirmwareVersionComparer.AreEquivalent(firmware.BiosVersionLabel, biosMatch.Version)
                ? $"Current BIOS {firmware.BiosVersionLabel} matches the latest BIOS version listed on HP Support for {productMatch.DisplayTitle}."
                : $"Official HP Support lists BIOS {biosMatch.Version} dated {biosMatch.ReleaseDate?.ToLocalTime().ToString("d") ?? "unknown date"} for {productMatch.DisplayTitle}, while this machine reports {firmware.BiosVersionLabel}. Confirm the exact platform family and review the HP release notes before flashing.";

            return new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.CatalogFeed,
                "HP",
                productMatch.DisplayTitle,
                firmware.BiosVersionLabel,
                firmware.BiosReleaseDate,
                $"Latest BIOS information was verified from HP Support for {productMatch.DisplayTitle}.",
                "Use HP Support, HP Image Assistant, or HP CMSL on the exact business system path. Review the HP release notes and keep rollback plus power-safety gates explicit before any flash.",
                comparisonSummary,
                $"Use HP Support, HP Image Assistant, or HP CMSL to confirm {productMatch.DisplayTitle} before applying BIOS {biosMatch.Version}.",
                productMatch.DriversPageUrl,
                biosMatch.DetailsUrl ?? biosMatch.PackageUrl ?? productMatch.DriversPageUrl,
                biosMatch.Version,
                biosMatch.ReleaseDate,
                "HP Image Assistant / HP CMSL",
                $"The comparison is grounded on official HP Support typeahead, OS catalog, and driver feed data for {osSelection.OsVersionName}. HP Image Assistant or CMSL remains the preferred execution path for managed fleets.",
                "Official HP Support typeahead, osVersionData, and driverDetails feed",
                null,
                DateTimeOffset.Now,
                false,
                biosMatch.Title,
                biosMatch.Description);
        }
        catch (Exception ex)
        {
            return BuildHpWorkflow(
                firmware,
                supportSearchUrl,
                $"The official HP latest-BIOS lookup failed: {ex.Message}");
        }
    }

    private static FirmwareReleaseLookupResult BuildHpWorkflow(
        FirmwareInventorySnapshot firmware,
        string? supportUrl = null,
        string? warningMessage = null) =>
        new(
            FirmwareReleaseLookupMode.VendorToolWorkflow,
            "HP",
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "Official HP firmware tooling is ready, but the app did not auto-claim a latest BIOS release for this exact machine.",
            "Use HP Support, HP Support Assistant, or for business PCs HP Image Assistant / HP CMSL. Keep BIOS validation on official HP tooling or support pages only.",
            "Latest BIOS version is not auto-verified for HP here. The correct workflow is the official HP support or enterprise tool path.",
            $"Open HP Support or the official HP driver portal for \"{firmware.SupportModel}\" before comparing BIOS packages.",
            supportUrl ?? "https://support.hp.com/us-en/drivers",
            supportUrl ?? "https://support.hp.com/us-en/drivers",
            null,
            null,
            "HP Image Assistant / HP CMSL",
            "For business systems, HP Image Assistant and HP CMSL are the right official BIOS-management path. HP Support Assistant is the end-user route.",
            "Official HP support and enterprise tooling",
            warningMessage,
            DateTimeOffset.Now);

    private async Task<FirmwareReleaseLookupResult> LookupLenovoAsync(
        FirmwareInventorySnapshot firmware,
        CancellationToken cancellationToken)
    {
        string searchUrl = $"https://pcsupport.lenovo.com/us/en/search?query={Uri.EscapeDataString(firmware.SupportModel)}";
        string searchApiUrl = $"https://pcsupport.lenovo.com/us/en/search/IndexPCG?query={Uri.EscapeDataString(firmware.SupportModel)}&page=1&l=en&Sort=score%20desc&realm=REALM.PCG&countries=us&containCommunity=true&containSR=true";

        try
        {
            string searchJson = await GetResponseTextAsync(searchApiUrl, searchUrl, cancellationToken);
            LenovoProductSearchMatch? productMatch = LenovoSupportCatalogResolver.ResolveBestProductMatch(searchJson, firmware.SupportModel);
            if (productMatch is null)
            {
                return BuildLenovoWorkflow(
                    firmware,
                    searchUrl,
                    $"Official Lenovo search did not resolve an exact product match for {firmware.SupportModel}. Keep BIOS validation on the exact Lenovo support or utility path.");
            }

            string downloadsApiUrl = $"https://pcsupport.lenovo.com/us/en/api/v4/downloads/drivers?productId={Uri.EscapeDataString(productMatch.ProductId)}";
            string downloadsJson = await GetResponseTextAsync(downloadsApiUrl, productMatch.DriversPageUrl, cancellationToken);
            LenovoBiosReleaseMatch? biosMatch = LenovoSupportCatalogResolver.ResolveLatestBios(downloadsJson);
            if (biosMatch is null)
            {
                return BuildLenovoWorkflow(
                    firmware,
                    productMatch.DriversPageUrl,
                    $"Official Lenovo support resolved {productMatch.DisplayTitle}, but no deterministic BIOS update item was auto-selected from the downloads feed.");
            }

            string comparisonSummary = FirmwareVersionComparer.AreEquivalent(firmware.BiosVersionLabel, biosMatch.Version)
                ? $"Current BIOS {firmware.BiosVersionLabel} matches the latest BIOS version listed on Lenovo Support for {productMatch.DisplayTitle}."
                : $"Official Lenovo Support lists BIOS {biosMatch.Version} dated {biosMatch.ReleaseDate?.ToLocalTime().ToString("d") ?? "unknown date"} for {productMatch.DisplayTitle}, while this machine reports {firmware.BiosVersionLabel}. Confirm the exact machine type or serial and review the Lenovo readme before flashing.";

            return new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.CatalogFeed,
                "Lenovo",
                productMatch.DisplayTitle,
                firmware.BiosVersionLabel,
                firmware.BiosReleaseDate,
                $"Latest BIOS information was verified from Lenovo Support for {productMatch.DisplayTitle}.",
                "Use Lenovo Support, Lenovo Vantage, or Lenovo System Update on the exact machine type or serial path. Review the official Lenovo readme and keep rollback plus power-safety gates explicit before any flash.",
                comparisonSummary,
                $"Use Lenovo Support or Lenovo System Update to confirm {productMatch.DisplayTitle} before applying BIOS {biosMatch.Version}.",
                productMatch.DriversPageUrl,
                biosMatch.DetailsUrl ?? productMatch.DriversPageUrl,
                biosMatch.Version,
                biosMatch.ReleaseDate,
                "Lenovo Vantage / System Update",
                "Lenovo Vantage or System Update remains the official convenience workflow, but this comparison is grounded on Lenovo Support's product search and downloads API for the matched machine type.",
                "Official Lenovo Support search and downloads API",
                null,
                DateTimeOffset.Now,
                false,
                biosMatch.Title,
                biosMatch.Summary);
        }
        catch (Exception ex)
        {
            return BuildLenovoWorkflow(
                firmware,
                searchUrl,
                $"The official Lenovo latest-BIOS lookup failed: {ex.Message}");
        }
    }

    private static FirmwareReleaseLookupResult BuildLenovoWorkflow(
        FirmwareInventorySnapshot firmware,
        string? supportUrl = null,
        string? warningMessage = null)
    {
        string searchUrl = supportUrl ?? $"https://pcsupport.lenovo.com/us/en/search?query={Uri.EscapeDataString(firmware.SupportModel)}";

        return new FirmwareReleaseLookupResult(
            FirmwareReleaseLookupMode.VendorToolWorkflow,
            "Lenovo",
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "Official Lenovo support is ready, but the app did not auto-claim a latest BIOS release for this exact machine.",
            "Use Lenovo Support, Lenovo Vantage, or Lenovo System Update on the exact machine type or serial path. Keep BIOS validation on Lenovo tooling or support pages.",
            "Latest BIOS version is not auto-verified for Lenovo here. The correct workflow is the official Lenovo support or utility path.",
            $"Search Lenovo support for \"{firmware.SupportModel}\" and verify the exact machine type or serial before comparing BIOS releases.",
            searchUrl,
            searchUrl,
            null,
            null,
            "Lenovo Vantage / System Update",
            "Lenovo's official client workflow is Lenovo Vantage or System Update. Use pcsupport for the exact model or serial, not guessed download URLs.",
            "Official Lenovo support route",
            warningMessage,
            DateTimeOffset.Now);
    }

    private async Task<string> GetResponseTextAsync(
        string url,
        string? referer,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(DefaultUserAgent);
        if (!string.IsNullOrWhiteSpace(referer))
        {
            request.Headers.Referrer = new Uri(referer);
        }

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<string> PostJsonAsync(
        string url,
        string? referer,
        object payload,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.UserAgent.ParseAdd(DefaultUserAgent);
        if (!string.IsNullOrWhiteSpace(referer))
        {
            request.Headers.Referrer = new Uri(referer);
        }

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string BuildHpTypeaheadUrl(string query) =>
        $"https://support.hp.com/typeahead?q={Uri.EscapeDataString(query)}&resultLimit=10&store=tmsstore&languageCode=en&filters={Uri.EscapeDataString("class:(pm_series_value^1.1 OR pm_name_value OR pm_number_value) AND (hiddenproduct:no OR (!_exists_:hiddenproduct))")}&printFields={Uri.EscapeDataString("tmspmseriesvalue,tmspmnamevalue,tmspmnumbervalue,class,productid,title,tmsnodedepth,seofriendlyname,navigationpath,shortestnavigationpath,childnodes,activewebsupportflag,historicalwebsupportflag,body,btoflag,description")}";

    private static FirmwareReleaseLookupResult BuildGenericWorkflow(FirmwareInventorySnapshot firmware) =>
        new(
            FirmwareReleaseLookupMode.ManualReview,
            firmware.SupportManufacturer,
            firmware.SupportIdentityLabel,
            firmware.BiosVersionLabel,
            firmware.BiosReleaseDate,
            "The app prepared the official vendor route, but no safe latest-BIOS automation is mapped for this vendor yet.",
            "Use the official vendor support route, compare the current BIOS against the listed firmware manually, and keep the decision on a technician review path.",
            "Latest BIOS version is not auto-verified for this vendor yet.",
            firmware.SupportSearchHint,
            firmware.PrimarySupportUrl,
            firmware.PrimarySupportUrl,
            null,
            null,
            null,
            null,
            "Official vendor support route",
            null,
            DateTimeOffset.Now);

    private static FirmwareReleaseProbe? ParseAsusSupportPage(string html)
    {
        Match[] versionMatches = VersionRegex.Matches(html)
            .Cast<Match>()
            .Where(match => IsUsableVersion(match.Groups[1].Value))
            .ToArray();

        if (versionMatches.Length == 0)
        {
            return null;
        }

        Match versionMatch = versionMatches[0];
        int segmentEnd = versionMatches.Length > 1 ? versionMatches[1].Index : html.Length;
        string segment = html.Substring(versionMatch.Index, Math.Max(0, segmentEnd - versionMatch.Index));
        Match dateMatch = DateRegex.Match(segment);

        DateTimeOffset? releaseDate = null;
        if (dateMatch.Success
            && DateTimeOffset.TryParseExact(
                dateMatch.Value,
                "yyyy/MM/dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal,
                out DateTimeOffset parsedDate))
        {
            releaseDate = parsedDate;
        }

        bool isBeta = BetaRegex.IsMatch(segment);
        return new FirmwareReleaseProbe(versionMatch.Groups[1].Value, releaseDate, isBeta);
    }

    private static bool IsUsableVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length < 2)
        {
            return false;
        }

        if (string.Equals(trimmed, "to", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.Any(char.IsDigit);
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        return client;
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

    private sealed record FirmwareReleaseProbe(
        string Version,
        DateTimeOffset? ReleaseDate,
        bool IsBeta);

    private const string DefaultUserAgent = "AegisTune/1.0 (+https://ichiphost.gr)";

    private static readonly Regex VersionRegex = new(
        @"Version\s+([A-Za-z0-9._-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DateRegex = new(
        @"\b20\d{2}/\d{2}/\d{2}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BetaRegex = new(
        @"\bBeta\s+Version\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
        ["gigabytetechnologycoltd"] = "gigabyte"
    };
}
