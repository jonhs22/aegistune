using AegisTune.Core;
using AegisTune.DriverEngine;

namespace AegisTune.Core.Tests;

public sealed class PnpUtilDriverStoreEvidenceServiceTests
{
    [Fact]
    public async Task CollectAsync_ParsesInstalledAndOutrankedDriversFromXml()
    {
        FakeDriverQueryRunner runner = new(
            0,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <PnpUtil Version="10.0.26300" Command="/enum-devices /instanceid PCI\VEN_10EC&amp;DEV_8168 /drivers /format xml">
                <Device InstanceId="PCI\VEN_10EC&amp;DEV_8168">
                    <DeviceDescription>Realtek PCIe GbE Family Controller</DeviceDescription>
                    <Status>Started</Status>
                    <DriverName>oem30.inf</DriverName>
                    <MatchingDrivers>
                        <DriverName DriverName="oem30.inf">
                            <OriginalName>rt640x64.inf</OriginalName>
                            <ProviderName>Realtek</ProviderName>
                            <DriverVersion>03/15/2021 10.48.315.2021</DriverVersion>
                            <SignerName>Microsoft Windows Hardware Compatibility Publisher</SignerName>
                            <MatchingDeviceId>PCI\VEN_10EC&amp;DEV_8168&amp;SUBSYS_86771043&amp;REV_15</MatchingDeviceId>
                            <Rank>00FF0000</Rank>
                            <Status>BestRanked/Installed</Status>
                        </DriverName>
                        <DriverName DriverName="rtcx21x64.inf">
                            <ProviderName>Microsoft</ProviderName>
                            <DriverVersion>08/10/2017 1.0.0.14</DriverVersion>
                            <SignerName>Microsoft Windows</SignerName>
                            <MatchingDeviceId>PCI\VEN_10EC&amp;DEV_8168&amp;SUBSYS_86771043&amp;REV_15</MatchingDeviceId>
                            <Rank>00FF1000</Rank>
                            <Status>Outranked</Status>
                        </DriverName>
                    </MatchingDrivers>
                </Device>
            </PnpUtil>
            """);
        PnpUtilDriverStoreEvidenceService service = new(runner);

        DriverStoreDeviceEvidenceResult result = await service.CollectAsync(CreateDevice());

        Assert.Equal("pnputil.exe", runner.LastFileName);
        Assert.Equal("/enum-devices /instanceid \"PCI\\VEN_10EC&DEV_8168\" /drivers /format xml", runner.LastArguments);
        Assert.True(result.QuerySucceeded);
        Assert.True(result.DeviceFound);
        Assert.Equal("oem30.inf", result.ReportedDriverName);
        Assert.Equal(2, result.MatchingDriverCount);
        Assert.Equal(1, result.OutrankedDriverCount);
        Assert.Equal("Started", result.DeviceStatus);
        Assert.Equal("oem30.inf (rt640x64.inf) • Realtek • 03/15/2021 10.48.315.2021 • BestRanked/Installed • 00FF0000", result.InstalledDriverSummary);
        Assert.Contains("rtcx21x64.inf", result.MatchingDriversPreview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectAsync_WhenPnPUtilFindsNoDevice_ReturnsNoEvidence()
    {
        FakeDriverQueryRunner runner = new(
            0,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <PnpUtil Version="10.0.26300" Command="/enum-devices /instanceid INVALID /drivers /format xml">
                <Message>No devices were found on the system.</Message>
            </PnpUtil>
            No devices were found on the system.
            """);
        PnpUtilDriverStoreEvidenceService service = new(runner);

        DriverStoreDeviceEvidenceResult result = await service.CollectAsync(CreateDevice());

        Assert.True(result.QuerySucceeded);
        Assert.False(result.DeviceFound);
        Assert.Empty(result.MatchingDrivers);
        Assert.Contains("did not find a device", result.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    private static DriverDeviceRecord CreateDevice() =>
        new(
            "Realtek PCIe GbE Family Controller",
            "Net",
            "Realtek",
            "Realtek",
            "10.48.315.2021",
            "OK",
            0,
            "PCI\\VEN_10EC&DEV_8168");

    private sealed class FakeDriverQueryRunner : IDriverQueryRunner
    {
        private readonly int _exitCode;
        private readonly string _standardOutput;

        public FakeDriverQueryRunner(int exitCode, string standardOutput)
        {
            _exitCode = exitCode;
            _standardOutput = standardOutput;
        }

        public string? LastFileName { get; private set; }

        public string? LastArguments { get; private set; }

        public Task<DriverQueryExecutionResult> RunAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken = default)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            return Task.FromResult(new DriverQueryExecutionResult(_exitCode, _standardOutput, string.Empty));
        }
    }
}
