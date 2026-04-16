using System.Text;
using System.Text.RegularExpressions;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class PnpUtilDriverRepositorySeedService : IDriverRepositorySeedService
{
    private static readonly Regex OemInfPattern = new(
        @"^oem\d+\.inf$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IDriverCommandRunner _commandRunner;

    public PnpUtilDriverRepositorySeedService(IDriverCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public async Task<DriverRepositorySeedResult> ExportInstalledPackageAsync(
        DriverDeviceRecord device,
        string targetRoot,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        string infName = device.InfName ?? string.Empty;
        string sanitizedTargetRoot = Path.GetFullPath(targetRoot);
        string exportDirectory = BuildExportDirectory(device, sanitizedTargetRoot);
        string commandLine = $"pnputil.exe {BuildArguments(infName, exportDirectory)}";
        DateTimeOffset executedAt = DateTimeOffset.Now;

        if (string.IsNullOrWhiteSpace(infName))
        {
            return new DriverRepositorySeedResult(
                infName,
                sanitizedTargetRoot,
                exportDirectory,
                commandLine,
                dryRunEnabled,
                false,
                null,
                executedAt,
                "The selected device does not expose an installed INF package name.",
                "Only installed third-party OEM packages can be seeded from the driver store.");
        }

        if (!OemInfPattern.IsMatch(infName))
        {
            return new DriverRepositorySeedResult(
                infName,
                sanitizedTargetRoot,
                exportDirectory,
                commandLine,
                dryRunEnabled,
                false,
                null,
                executedAt,
                $"The installed package {infName} is not an exportable third-party OEM driver package.",
                "In-box Microsoft packages cannot be exported with pnputil /export-driver. Use a vetted OEM repository instead.");
        }

        if (!Directory.Exists(sanitizedTargetRoot))
        {
            return new DriverRepositorySeedResult(
                infName,
                sanitizedTargetRoot,
                exportDirectory,
                commandLine,
                dryRunEnabled,
                false,
                null,
                executedAt,
                "The target driver repository root is not currently accessible.",
                "Verify the repository path in Settings before seeding the local depot.");
        }

        if (dryRunEnabled)
        {
            return new DriverRepositorySeedResult(
                infName,
                sanitizedTargetRoot,
                exportDirectory,
                commandLine,
                true,
                true,
                null,
                executedAt,
                $"Dry-run mode is enabled. AegisTune did not export {infName} into the local repository.",
                $"Disable dry-run to export the installed package into {exportDirectory}.");
        }

        Directory.CreateDirectory(exportDirectory);
        int exitCode = await _commandRunner.RunElevatedAsync(
            "pnputil.exe",
            BuildArguments(infName, exportDirectory),
            cancellationToken);

        bool succeeded = exitCode == 0;
        return new DriverRepositorySeedResult(
            infName,
            sanitizedTargetRoot,
            exportDirectory,
            commandLine,
            false,
            succeeded,
            exitCode,
            executedAt,
            succeeded
                ? $"Exported {infName} into the local driver repository."
                : $"PnPUtil exited with code {exitCode} while exporting {infName}.",
            succeeded
                ? "Refresh the Driver Center to re-scan the repository and confirm the exported INF now appears as a local candidate."
                : "Review the installed package, repository root, and elevation context before trying the export again.");
    }

    public static bool CanExportInstalledPackage(DriverDeviceRecord? device) =>
        device is not null
        && !string.IsNullOrWhiteSpace(device.InfName)
        && OemInfPattern.IsMatch(device.InfName);

    public static string BuildArguments(string infName, string exportDirectory) =>
        $"/export-driver \"{infName}\" \"{exportDirectory}\"";

    private static string BuildExportDirectory(DriverDeviceRecord device, string targetRoot)
    {
        string sanitizedClass = SanitizePathPart(string.IsNullOrWhiteSpace(device.DeviceClass) ? "unknown-class" : device.DeviceClass);
        string sanitizedDevice = SanitizePathPart(string.IsNullOrWhiteSpace(device.FriendlyName) ? "device" : device.FriendlyName);
        string infSlug = SanitizePathPart(Path.GetFileNameWithoutExtension(device.InfName ?? "oem"));
        return Path.Combine(
            targetRoot,
            "seeded-driver-store",
            sanitizedClass,
            $"{sanitizedDevice}-{infSlug}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
    }

    private static string SanitizePathPart(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString().Trim().Trim('-');
    }
}
