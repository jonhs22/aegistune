using System.Net;
using System.Net.Http;
using AegisTune.Core;
using AegisTune.SystemIntegration;

namespace AegisTune.Core.Tests;

public sealed class OfficialFirmwareReleaseLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_ParsesLatestAsusVersionFromOfficialSupportPage()
    {
        const string html = """
<!doctype html>
<html>
<head><title>TUF B450-PLUS GAMING - Support</title></head>
<body>
<div>BIOS &amp; FIRMWARE</div>
<div>Version 4801</div>
<div>2026/04/01</div>
<div>DOWNLOAD</div>
<div>Version 4645</div>
<div>2026/02/02</div>
</body>
</html>
""";

        HttpClient client = new(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("ASUS", "TUF B450-PLUS GAMING", "4645"));

        Assert.Equal(FirmwareReleaseLookupMode.DirectVendorPage, result.Mode);
        Assert.Equal("4801", result.LatestVersion);
        Assert.Equal(new DateTime(2026, 4, 1), result.LatestReleaseDate?.Date);
        Assert.Contains("4801", result.ComparisonSummary);
        Assert.Contains("4645", result.ComparisonSummary);
        Assert.Contains("official ASUS support", result.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAsync_FlagsBetaAsusListings()
    {
        const string html = """
<!doctype html>
<html>
<body>
<div>Version 4645</div>
<div>Beta Version</div>
<div>2026/02/02</div>
</body>
</html>
""";

        HttpClient client = new(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("ASUS", "TUF B450-PLUS GAMING", "4204"));

        Assert.True(result.LatestIsBeta);
        Assert.Contains("Beta", result.LatestVersionLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Beta", result.GuidanceLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Beta", result.ComparisonSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAsync_FallsBackToSecondAsusUrlAfterFirstProbeFails()
    {
        int callCount = 0;
        HttpClient client = new(new StubMessageHandler(request =>
        {
            callCount++;
            if (request.RequestUri?.AbsoluteUri.Contains("/us/supportonly/", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new HttpRequestException("Primary ASUS URL failed.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
<!doctype html>
<html>
<body>
<div>Version 4801</div>
<div>2026/04/01</div>
</body>
</html>
""")
            };
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("ASUS", "TUF B450-PLUS GAMING", "4645"));

        Assert.Equal(2, callCount);
        Assert.Equal(FirmwareReleaseLookupMode.DirectVendorPage, result.Mode);
        Assert.Equal("4801", result.LatestVersion);
    }

    [Fact]
    public async Task LookupAsync_UsesDellCatalogWorkflowWithoutClaimingLatestVersion()
    {
        OfficialFirmwareReleaseLookupService service = new(
            new HttpClient(new StubMessageHandler(_ => throw new InvalidOperationException("Network should not be used for Dell workflow test."))),
            new StubDellCatalogSource(null));

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("Dell", "Latitude 5550", "1.12.0"));

        Assert.Equal(FirmwareReleaseLookupMode.CatalogFeed, result.Mode);
        Assert.False(result.HasLatestRelease);
        Assert.Equal("Dell Command | Update", result.ToolTitle);
        Assert.Contains("catalog", result.GuidanceLine, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("https://www.dell.com/support/home/en-us", result.SupportUrl);
    }

    [Fact]
    public async Task LookupAsync_ParsesLatestDellVersionFromOfficialCatalogWithoutFalseE5550Match()
    {
        const string xml = """
<?xml version="1.0" encoding="utf-16"?>
<Manifest>
  <SoftwareComponent packageID="OLD1" releaseDate="June 29, 2020" vendorVersion="A24" dellVersion="A24">
    <Name><Display lang="en"><![CDATA[Dell Latitude E5550 System BIOS,A24,A24]]></Display></Name>
    <ComponentType value="BIOS"><Display lang="en"><![CDATA[BIOS]]></Display></ComponentType>
    <SupportedSystems>
      <Brand key="4" prefix="LAT">
        <Display lang="en"><![CDATA[Latitude]]></Display>
        <Model systemID="0A10">
          <Display lang="en"><![CDATA[E5550_5550]]></Display>
        </Model>
      </Brand>
    </SupportedSystems>
    <ImportantInfo URL="http://www.dell.com/support/home/us/en/19/Drivers/DriversDetails?driverId=OLD1" />
  </SoftwareComponent>
  <SoftwareComponent packageID="NEW1" releaseDate="February 09, 2026" vendorVersion="1.20.0" dellVersion="1.20.0">
    <Name><Display lang="en"><![CDATA[Dell Precision 3590/3591 and Latitude 5550 System BIOS,1.20.0,1.20.0]]></Display></Name>
    <ComponentType value="BIOS"><Display lang="en"><![CDATA[BIOS]]></Display></ComponentType>
    <SupportedSystems>
      <Brand key="4" prefix="LAT">
        <Display lang="en"><![CDATA[Latitude]]></Display>
        <Model systemID="0BCD">
          <Display lang="en"><![CDATA[Latitude-5550]]></Display>
        </Model>
      </Brand>
    </SupportedSystems>
    <ImportantInfo URL="http://www.dell.com/support/home/us/en/19/Drivers/DriversDetails?driverId=NEW1" />
  </SoftwareComponent>
</Manifest>
""";

        OfficialFirmwareReleaseLookupService service = new(
            new HttpClient(new StubMessageHandler(_ => throw new InvalidOperationException("Network should not be used for Dell workflow test."))),
            new StubDellCatalogSource(xml));

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("Dell", "Latitude 5550", "1.10.0"));

        Assert.Equal(FirmwareReleaseLookupMode.CatalogFeed, result.Mode);
        Assert.Equal("1.20.0", result.LatestVersion);
        Assert.Equal(new DateTime(2026, 2, 9), result.LatestReleaseDate?.Date);
        Assert.Equal("https://www.dell.com/support/home/us/en/19/Drivers/DriversDetails?driverId=NEW1".Replace("http://", "https://", StringComparison.OrdinalIgnoreCase), result.DetailsUrl);
        Assert.Contains("Latitude-5550", result.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("E5550_5550", result.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAsync_ParsesLatestLenovoVersionFromOfficialSupportApi()
    {
        const string searchJson = """
{
  "Results": [
    {
      "Type": "Product",
      "ID": "LAPTOPS-AND-NETBOOKS/THINKPAD-T-SERIES-LAPTOPS/THINKPAD-T14-GEN-5-TYPE-21ML-21MM",
      "Title": "<em>T14</em> <em>Gen</em> <em>5</em> (Type 21ML, 21MM) Laptops (<em>ThinkPad</em>)"
    },
    {
      "Type": "Product",
      "ID": "LAPTOPS-AND-NETBOOKS/THINKPAD-T-SERIES-LAPTOPS/THINKPAD-T14-GEN-5-TYPE-21ML-21MM/21MM",
      "Title": "<em>T14</em> <em>Gen</em> <em>5</em> (Type 21ML, 21MM) Laptops (<em>ThinkPad</em>) - Type 21MM"
    },
    {
      "Type": "Product",
      "ID": "LAPTOPS-AND-NETBOOKS/THINKPAD-T-SERIES-LAPTOPS/THINKPAD-T14S-GEN-5-TYPE-21LS-21LT/21LT",
      "Title": "<em>T14s</em> <em>Gen</em> <em>5</em> (Type 21LS, 21LT) Laptop (<em>ThinkPad</em>) - Type 21LT"
    }
  ]
}
""";

        const string downloadsJson = """
{
  "message": "succeed",
  "body": {
    "DownloadItems": [
      {
        "Title": "ThinkPad Setup Settings Capture/Playback Utility for Windows (SRSETUPWIN) for Windows 11 (64-bit), 10 (64-bit) - ThinkPad",
        "Summary": "ThinkPad stores various UEFI BIOS settings in its nonvolatile memory.",
        "DocId": "DS555772",
        "Date": { "Unix": 1766110680000 },
        "SummaryInfo": { "Priority": "Recommended", "Version": "5.12" },
        "Category": { "Name": "BIOS/UEFI" },
        "Files": [
          { "Name": "ThinkPad Setup Settings Capture/Playback Utility for Windows (SRSETUPWIN)", "TypeString": "zip", "Version": "5.12", "URL": "https://download.lenovo.com/pccbbs/mobiles/n3xts04w.zip" }
        ]
      },
      {
        "Title": "BIOS Update (Utility & Bootable CD) for Windows 11, 10 (64-bit) - ThinkPad T14 Gen 5 (Type 21ML, 21MM)",
        "Summary": "This package updates the UEFI BIOS (Utility & Bootable CD) for Windows 11, 10 64-bit.",
        "DocId": "DS569002",
        "Date": { "Unix": 1773216660000 },
        "SummaryInfo": { "Priority": "Critical", "Version": "1.17" },
        "Category": { "Name": "BIOS/UEFI" },
        "Files": [
          { "Name": "BIOS Update Utility", "TypeString": "EXE", "Version": "1.17", "URL": "https://download.lenovo.com/pccbbs/mobiles/n47uj12w.exe" },
          { "Name": "README for BIOS Update Utility", "TypeString": "HTML", "Version": "1.17", "URL": "https://download.lenovo.com/pccbbs/mobiles/n47uj12w.html" }
        ]
      }
    ]
  }
}
""";

        HttpClient client = new(new StubMessageHandler(request =>
        {
            string url = request.RequestUri?.AbsoluteUri ?? string.Empty;

            return url.Contains("/search/IndexPCG", StringComparison.OrdinalIgnoreCase)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(searchJson) }
                : url.Contains("/api/v4/downloads/drivers", StringComparison.OrdinalIgnoreCase)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(downloadsJson) }
                    : throw new InvalidOperationException($"Unexpected Lenovo request: {url}");
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("Lenovo", "ThinkPad T14 Gen 5 21MM", "N47ET17W (1.17)"));

        Assert.Equal(FirmwareReleaseLookupMode.CatalogFeed, result.Mode);
        Assert.Equal("1.17", result.LatestVersion);
        Assert.Equal(new DateTime(2026, 3, 11), result.LatestReleaseDate?.Date);
        Assert.Equal("https://download.lenovo.com/pccbbs/mobiles/n47uj12w.html", result.DetailsUrl);
        Assert.Contains("BIOS Update", result.LatestReleaseTitleLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("updates the UEFI BIOS", result.LatestReleaseNotesSummaryLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/21mm/downloads/driver-list", result.SupportUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matches the latest BIOS", result.ComparisonSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAsync_FallsBackToLenovoWorkflowWhenOfficialSearchCannotResolveExactProduct()
    {
        const string searchJson = """
{
  "Results": [
    {
      "Type": "Product",
      "ID": "LAPTOPS-AND-NETBOOKS/THINKPAD-T-SERIES-LAPTOPS/THINKPAD-T14S-GEN-5-TYPE-21LS-21LT/21LT",
      "Title": "<em>T14s</em> <em>Gen</em> <em>5</em> (Type 21LS, 21LT) Laptop (<em>ThinkPad</em>) - Type 21LT"
    }
  ]
}
""";

        HttpClient client = new(new StubMessageHandler(request =>
        {
            string url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("/search/IndexPCG", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(searchJson)
                };
            }

            throw new InvalidOperationException($"Unexpected Lenovo request: {url}");
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("Lenovo", "ThinkPad T14 Gen 5 21MM", "N47ET17W (1.17)"));

        Assert.Equal(FirmwareReleaseLookupMode.VendorToolWorkflow, result.Mode);
        Assert.False(result.HasLatestRelease);
        Assert.Contains("Lenovo", result.ToolTitle ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not resolve an exact product match", result.WarningMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAsync_ParsesLatestHpVersionFromOfficialSupportApi()
    {
        const string typeaheadJson = """
{
  "matches": [
    {
      "name": "HP EliteBook 840 G8 Certified Refurbished Notebook PC",
      "pmClass": "pm_series_value",
      "pmSeriesOid": 2103308461,
      "activeWebSupportFlag": "yes",
      "seoFriendlyName": "hp-elitebook-840-g8-certified-refurbished-notebook-pc",
      "productId": 2103308461
    },
    {
      "name": "HP EliteBook 840 G8 Notebook PC (4V7X0AV)",
      "pmClass": "pm_name_value",
      "pmSeriesOid": 38216725,
      "activeWebSupportFlag": "yes",
      "seoFriendlyName": "hp-elitebook-840-g8-notebook-pc",
      "productId": 2100970629,
      "btoFlag": true
    },
    {
      "name": "HP EliteBook 840 G8 Notebook PC",
      "pmClass": "pm_series_value",
      "pmSeriesOid": 38216725,
      "activeWebSupportFlag": "yes",
      "seoFriendlyName": "hp-elitebook-840-g8-notebook-pc",
      "productId": 38216725
    }
  ]
}
""";

        const string osVersionJson = """
{
  "data": {
    "osversions": [
      {
        "name": "Windows 10",
        "osVersionList": [
          {
            "id": "w10-x64",
            "name": "Windows 10 (64-bit)"
          }
        ]
      },
      {
        "name": "Windows 11",
        "osVersionList": [
          {
            "id": "w11-generic",
            "name": "Windows 11"
          },
          {
            "id": "w11-24h2",
            "name": "Windows 11 version 24H2 (64-bit)"
          }
        ]
      }
    ]
  }
}
""";

        const string driverDetailsJson = """
{
  "data": {
    "softwareTypes": [
      {
        "accordionName": "Software-Solutions",
        "tmsName": "Software",
        "softwareDriversList": [
          {
            "latestVersionDriver": {
              "title": "HP Wolf Security Console",
              "version": "11.1.4.895 Rev.A",
              "releaseDate": "2025-07-01T03:00:00+03:00",
              "softwareItemId": "ob-350148-1",
              "detailInformation": {
                "description": "HP Wolf Security Console provides configuration."
              }
            }
          }
        ]
      },
      {
        "accordionName": "BIOS-System Firmware",
        "tmsName": "BIOS",
        "softwareDriversList": [
          {
            "latestVersionDriver": {
              "title": "HP BIOS and System Firmware (T37/T39/T76)",
              "version": "01.24.01 Rev.A",
              "releaseDate": "2026-04-02T03:00:00+03:00",
              "fileUrl": "https://ftp.hp.com/pub/softpaq/sp172001-172500/sp172011.exe",
              "softwareItemId": "ob-361835-1",
              "detailInformation": {
                "description": "This package creates files that contain an image of the System BIOS (ROM) for the supported computer models.",
                "releaseDate": "2026-04-02T03:00:00+03:00"
              }
            },
            "associatedContentList": [
              {
                "diskAttachmentLink": "https://support.hp.com/soar-attachment/144/col110274-ob-361835-1_sp172011_releasedoc.html"
              }
            ]
          }
        ]
      }
    ]
  }
}
""";

        string? postedDriverDetailsBody = null;
        HttpClient client = new(new StubMessageHandler(async (request, cancellationToken) =>
        {
            string url = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (url.Contains("/typeahead", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(typeaheadJson) };
            }

            if (url.Contains("/osVersionData", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(osVersionJson) };
            }

            if (request.Method == HttpMethod.Post && url.Contains("/driverDetails", StringComparison.OrdinalIgnoreCase))
            {
                postedDriverDetailsBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(driverDetailsJson) };
            }

            throw new InvalidOperationException($"Unexpected HP request: {request.Method} {url}");
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("HP", "HP EliteBook 840 G8 Notebook PC", "01.24.01"));

        Assert.Equal(FirmwareReleaseLookupMode.CatalogFeed, result.Mode);
        Assert.Equal("01.24.01 Rev.A", result.LatestVersion);
        Assert.Equal(new DateTime(2026, 4, 2), result.LatestReleaseDate?.Date);
        Assert.Equal("https://support.hp.com/soar-attachment/144/col110274-ob-361835-1_sp172011_releasedoc.html", result.DetailsUrl);
        Assert.Equal("HP BIOS and System Firmware (T37/T39/T76)", result.LatestReleaseTitleLabel);
        Assert.Contains("image of the System BIOS", result.LatestReleaseNotesSummaryLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/drivers/hp-elitebook-840-g8-notebook-pc/38216725", result.SupportUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matches the latest BIOS", result.ComparisonSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("HP Image Assistant / HP CMSL", result.ToolTitle);
        Assert.NotNull(postedDriverDetailsBody);
        Assert.Contains("\"productSeriesOid\":38216725", postedDriverDetailsBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("productNumberOid", postedDriverDetailsBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAsync_FallsBackToHpWorkflowWhenTypeaheadOnlyReturnsRefurbishedOrBtoMatches()
    {
        const string typeaheadJson = """
{
  "matches": [
    {
      "name": "HP EliteBook 840 G8 Certified Refurbished Notebook PC",
      "pmClass": "pm_series_value",
      "pmSeriesOid": 2103308461,
      "activeWebSupportFlag": "yes",
      "seoFriendlyName": "hp-elitebook-840-g8-certified-refurbished-notebook-pc",
      "productId": 2103308461
    },
    {
      "name": "HP EliteBook 840 G8 Notebook PC (4V7X0AV)",
      "pmClass": "pm_name_value",
      "pmSeriesOid": 38216725,
      "activeWebSupportFlag": "yes",
      "seoFriendlyName": "hp-elitebook-840-g8-notebook-pc",
      "productId": 2100970629,
      "btoFlag": true
    }
  ]
}
""";

        HttpClient client = new(new StubMessageHandler(request =>
        {
            string url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("/typeahead", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(typeaheadJson)
                };
            }

            throw new InvalidOperationException($"Unexpected HP request: {request.Method} {url}");
        }));

        OfficialFirmwareReleaseLookupService service = new(client);

        FirmwareReleaseLookupResult result = await service.LookupAsync(CreateFirmwareSnapshot("HP", "HP EliteBook 840 G8 Notebook PC", "01.20.00"));

        Assert.Equal(FirmwareReleaseLookupMode.VendorToolWorkflow, result.Mode);
        Assert.False(result.HasLatestRelease);
        Assert.Contains("did not resolve an exact product-series match", result.WarningMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("HP Image Assistant / HP CMSL", result.ToolTitle);
    }

    private static FirmwareInventorySnapshot CreateFirmwareSnapshot(string vendor, string model, string biosVersion) =>
        new(
            vendor,
            model,
            vendor,
            model,
            vendor,
            biosVersion,
            "Firmware family",
            new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
            "UEFI",
            true,
            vendor,
            model,
            "System model identity",
            $"Official {vendor} firmware route",
            "https://example.invalid/support",
            $"Search the official {vendor} support page for \"{model} BIOS {biosVersion}\" before any firmware change.",
            "Firmware route is ready.",
            Array.Empty<FirmwareSupportOption>(),
            new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero));

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this((request, _) => Task.FromResult(handler(request)))
        {
        }

        public StubMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }

    private sealed class StubDellCatalogSource(string? xml) : IDellCatalogSource
    {
        public Task<string?> GetCatalogXmlAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(xml);
    }
}
