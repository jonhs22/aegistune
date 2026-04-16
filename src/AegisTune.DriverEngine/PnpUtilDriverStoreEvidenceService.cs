using System.Xml.Linq;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class PnpUtilDriverStoreEvidenceService : IDriverStoreEvidenceService
{
    private readonly IDriverQueryRunner _queryRunner;

    public PnpUtilDriverStoreEvidenceService(IDriverQueryRunner queryRunner)
    {
        _queryRunner = queryRunner;
    }

    public async Task<DriverStoreDeviceEvidenceResult> CollectAsync(
        DriverDeviceRecord device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        string arguments = BuildArguments(device.InstanceId);
        string commandLine = $"pnputil.exe {arguments}";
        DriverQueryExecutionResult queryResult = await _queryRunner.RunAsync("pnputil.exe", arguments, cancellationToken);
        return Parse(device.InstanceId, commandLine, queryResult.ExitCode, queryResult.StandardOutput, queryResult.StandardError);
    }

    public static string BuildArguments(string instanceId) =>
        $"/enum-devices /instanceid \"{instanceId}\" /drivers /format xml";

    public static DriverStoreDeviceEvidenceResult Parse(
        string instanceId,
        string commandLine,
        int exitCode,
        string standardOutput,
        string standardError)
    {
        DateTimeOffset collectedAt = DateTimeOffset.Now;
        string rawOutput = string.IsNullOrWhiteSpace(standardOutput)
            ? standardError
            : standardOutput;

        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return new DriverStoreDeviceEvidenceResult(
                instanceId,
                commandLine,
                false,
                false,
                exitCode,
                collectedAt,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                rawOutput,
                "PnPUtil did not return any driver-store evidence for this device query.",
                "Re-run the query and confirm that pnputil is available, the device instance ID is valid, and the current session can read device details.");
        }

        if (rawOutput.Contains("No devices were found on the system.", StringComparison.OrdinalIgnoreCase))
        {
            return new DriverStoreDeviceEvidenceResult(
                instanceId,
                commandLine,
                exitCode == 0,
                false,
                exitCode,
                collectedAt,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                rawOutput,
                "PnPUtil did not find a device for the current instance ID during the driver-store query.",
                "The device may have been rebound, removed, or may require a reboot before the new driver-store state is visible.");
        }

        try
        {
            XDocument document = XDocument.Parse(rawOutput, LoadOptions.PreserveWhitespace);
            XElement? deviceElement = document.Root?.Element("Device");

            if (deviceElement is null)
            {
                return new DriverStoreDeviceEvidenceResult(
                    instanceId,
                    commandLine,
                    false,
                    false,
                    exitCode,
                    collectedAt,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    [],
                    rawOutput,
                    "PnPUtil returned XML, but the driver-store payload did not contain a device node.",
                    "Review the raw pnputil output before treating the post-install evidence as trustworthy.");
            }

            List<DriverStoreCandidateEvidence> matchingDrivers = deviceElement
                .Element("MatchingDrivers")?
                .Elements("DriverName")
                .Select(element => new DriverStoreCandidateEvidence(
                    element.Attribute("DriverName")?.Value ?? string.Empty,
                    element.Element("OriginalName")?.Value,
                    element.Element("ProviderName")?.Value,
                    element.Element("DriverVersion")?.Value,
                    element.Element("SignerName")?.Value,
                    element.Element("MatchingDeviceId")?.Value,
                    element.Element("Rank")?.Value,
                    element.Element("Status")?.Value))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.DriverName))
                .ToList()
                ?? [];

            string reportedDriverName = deviceElement.Element("DriverName")?.Value ?? string.Empty;
            string deviceDescription = deviceElement.Element("DeviceDescription")?.Value ?? string.Empty;
            string deviceStatus = deviceElement.Element("Status")?.Value ?? string.Empty;

            DriverStoreDeviceEvidenceResult result = new(
                deviceElement.Attribute("InstanceId")?.Value ?? instanceId,
                commandLine,
                exitCode == 0,
                true,
                exitCode,
                collectedAt,
                deviceDescription,
                deviceStatus,
                reportedDriverName,
                matchingDrivers,
                rawOutput,
                matchingDrivers.Count == 0
                    ? "PnPUtil found the device, but it did not report any matching driver-store candidates."
                    : $"PnPUtil captured {matchingDrivers.Count:N0} matching driver-store candidate(s) for {deviceDescription}.",
                matchingDrivers.Count == 0
                    ? "Treat this as inconclusive evidence and confirm the current driver state through Device Manager or a full device re-scan."
                    : "Use the installed and outranked driver-store entries to confirm which package is actually effective after the install attempt.");

            return result;
        }
        catch (Exception ex)
        {
            return new DriverStoreDeviceEvidenceResult(
                instanceId,
                commandLine,
                false,
                false,
                exitCode,
                collectedAt,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                rawOutput,
                "PnPUtil returned driver-store output that AegisTune could not parse.",
                $"Review the raw pnputil XML before trusting the result. Parser error: {ex.Message}");
        }
    }
}
