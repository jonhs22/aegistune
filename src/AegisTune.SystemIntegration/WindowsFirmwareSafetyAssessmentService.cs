using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public interface IBitLockerStatusProbe
{
    Task<BitLockerVolumeStatus> GetSystemDriveStatusAsync(
        string systemDrive,
        CancellationToken cancellationToken = default);
}

[SupportedOSPlatform("windows")]
public interface IPowerStatusProbe
{
    Task<SystemPowerSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record BitLockerVolumeStatus(
    bool? IsProtectionOn,
    string StatusLine,
    string Detail);

public sealed record SystemPowerSnapshot(
    bool? IsAcOnline,
    bool HasBattery,
    int? BatteryPercentage,
    string StatusLine,
    string Detail);

[SupportedOSPlatform("windows")]
public sealed class WindowsFirmwareSafetyAssessmentService : IFirmwareSafetyAssessmentService
{
    private readonly IBitLockerStatusProbe _bitLockerStatusProbe;
    private readonly IPowerStatusProbe _powerStatusProbe;

    public WindowsFirmwareSafetyAssessmentService(
        IBitLockerStatusProbe bitLockerStatusProbe,
        IPowerStatusProbe powerStatusProbe)
    {
        _bitLockerStatusProbe = bitLockerStatusProbe;
        _powerStatusProbe = powerStatusProbe;
    }

    public async Task<FirmwareSafetyAssessment> AssessAsync(
        FirmwareInventorySnapshot firmware,
        FirmwareReleaseLookupResult? lookupResult = null,
        CancellationToken cancellationToken = default)
    {
        string systemDrive = ResolveSystemDrive();

        Task<BitLockerVolumeStatus> bitLockerTask = _bitLockerStatusProbe.GetSystemDriveStatusAsync(systemDrive, cancellationToken);
        Task<SystemPowerSnapshot> powerTask = _powerStatusProbe.GetCurrentAsync(cancellationToken);

        BitLockerVolumeStatus bitLockerStatus = await bitLockerTask;
        SystemPowerSnapshot powerStatus = await powerTask;

        FirmwareSafetyGate[] gates =
        [
            BuildIdentityGate(firmware),
            BuildOfficialSourceGate(firmware, lookupResult),
            BuildBitLockerGate(systemDrive, bitLockerStatus),
            BuildPowerGate(powerStatus),
            BuildTechnicianGate(firmware, lookupResult)
        ];

        return new FirmwareSafetyAssessment(
            firmware.SupportIdentityLabel,
            systemDrive,
            bitLockerStatus.StatusLine,
            powerStatus.StatusLine,
            DateTimeOffset.Now,
            gates,
            null,
            bitLockerStatus.IsProtectionOn,
            powerStatus.IsAcOnline,
            powerStatus.HasBattery,
            powerStatus.BatteryPercentage);
    }

    private static FirmwareSafetyGate BuildIdentityGate(FirmwareInventorySnapshot firmware)
    {
        return firmware.SupportIdentitySourceLabel switch
        {
            "System model identity" => new FirmwareSafetyGate(
                "Identity source",
                FirmwareSafetyGateSeverity.Pass,
                "System model identity is strong enough for an OEM-backed firmware decision.",
                $"Windows exposed the machine as {firmware.SupportIdentityLabel}.",
                "Keep the current support identity in the technician handoff and confirm the exact chassis or board revision before flash."),
            "Baseboard fallback identity" => new FirmwareSafetyGate(
                "Identity source",
                FirmwareSafetyGateSeverity.Attention,
                "Windows fell back to the baseboard identity for firmware routing.",
                $"This machine exposed generic chassis strings, so the firmware route is using {firmware.SupportIdentityLabel}.",
                "Confirm the exact board revision on the physical board or the OEM support label before staging a BIOS update."),
            _ => new FirmwareSafetyGate(
                "Identity source",
                FirmwareSafetyGateSeverity.Block,
                "Windows did not expose a reliable machine identity for a safe firmware decision.",
                $"The current support identity source is {firmware.SupportIdentitySourceLabel}.",
                "Do not flash until the exact OEM model or board revision is confirmed manually.")
        };
    }

    private static FirmwareSafetyGate BuildOfficialSourceGate(
        FirmwareInventorySnapshot firmware,
        FirmwareReleaseLookupResult? lookupResult)
    {
        if (lookupResult is null)
        {
            return new FirmwareSafetyGate(
                "Official source comparison",
                FirmwareSafetyGateSeverity.Attention,
                "Latest BIOS verification has not been run yet.",
                $"The machine is routed to {firmware.SupportRouteLabel}, but no latest-release comparison is cached yet.",
                "Run Check latest BIOS or complete the vendor review manually before deciding on a flash target.");
        }

        bool latestMatchesCurrent = lookupResult.HasLatestRelease
            && FirmwareVersionComparer.AreEquivalent(lookupResult.CurrentVersion, lookupResult.LatestVersion);
        string betaClause = lookupResult.LatestIsBeta
            ? " The latest vendor listing is marked Beta and should stay on a technician-only path."
            : string.Empty;

        if (latestMatchesCurrent)
        {
            return new FirmwareSafetyGate(
                "Official source comparison",
                FirmwareSafetyGateSeverity.Block,
                "Current BIOS already matches the latest official listing.",
                $"{lookupResult.ComparisonSummary}{betaClause}",
                "Do not stage a BIOS flash unless the vendor documents a specific recovery or issue-remediation reason.");
        }

        if (lookupResult.HasLatestRelease)
        {
            return new FirmwareSafetyGate(
                "Official source comparison",
                FirmwareSafetyGateSeverity.Attention,
                "A target BIOS release is visible, but release-note review is still required.",
                $"{lookupResult.ComparisonSummary}{betaClause}",
                string.IsNullOrWhiteSpace(lookupResult.DetailsUrl)
                    ? "Review the official release notes, changelog, and board-revision constraints before any flash."
                    : $"Open {lookupResult.DetailsUrl} and compare the changelog against the current BIOS before any flash.");
        }

        return new FirmwareSafetyGate(
            "Official source comparison",
            FirmwareSafetyGateSeverity.Attention,
            "Automatic latest-release verification did not resolve to a deterministic target.",
            $"{lookupResult.ModeLabel}: {lookupResult.GuidanceLine}",
            "Stay on the official vendor support or tool workflow and record the chosen BIOS target manually before any flash.");
    }

    private static FirmwareSafetyGate BuildBitLockerGate(
        string systemDrive,
        BitLockerVolumeStatus bitLockerStatus)
    {
        if (bitLockerStatus.IsProtectionOn == true)
        {
            return new FirmwareSafetyGate(
                "BitLocker posture",
                FirmwareSafetyGateSeverity.Attention,
                bitLockerStatus.StatusLine,
                bitLockerStatus.Detail,
                $"Suspend BitLocker on {systemDrive} before the firmware flash window, confirm the recovery key is available, and re-enable protection after a healthy post-flash boot.");
        }

        if (bitLockerStatus.IsProtectionOn == false)
        {
            return new FirmwareSafetyGate(
                "BitLocker posture",
                FirmwareSafetyGateSeverity.Pass,
                bitLockerStatus.StatusLine,
                bitLockerStatus.Detail,
                "Keep the current storage-protection posture documented in the technician handoff.");
        }

        return new FirmwareSafetyGate(
            "BitLocker posture",
            FirmwareSafetyGateSeverity.Attention,
            bitLockerStatus.StatusLine,
            bitLockerStatus.Detail,
            $"Confirm BitLocker manually with manage-bde -status {systemDrive} or the BitLocker control surface before any flash.");
    }

    private static FirmwareSafetyGate BuildPowerGate(SystemPowerSnapshot powerStatus)
    {
        if (powerStatus.IsAcOnline == true)
        {
            return new FirmwareSafetyGate(
                "Power stability",
                FirmwareSafetyGateSeverity.Pass,
                powerStatus.StatusLine,
                powerStatus.Detail,
                "Keep the system on AC power for the full firmware window. If this is a desktop, verify the PSU or UPS path is stable.");
        }

        if (powerStatus.IsAcOnline == false && powerStatus.HasBattery)
        {
            return new FirmwareSafetyGate(
                "Power stability",
                FirmwareSafetyGateSeverity.Block,
                powerStatus.StatusLine,
                powerStatus.Detail,
                "Do not flash while the system is on battery. Connect AC power first and keep it attached for the full update and reboot sequence.");
        }

        return new FirmwareSafetyGate(
            "Power stability",
            FirmwareSafetyGateSeverity.Attention,
            powerStatus.StatusLine,
            powerStatus.Detail,
            "Confirm stable AC power or UPS coverage manually before any firmware flash window.");
    }

    private static FirmwareSafetyGate BuildTechnicianGate(
        FirmwareInventorySnapshot firmware,
        FirmwareReleaseLookupResult? lookupResult)
    {
        string targetClause = lookupResult?.HasLatestRelease == true
            ? $"Current BIOS {lookupResult.CurrentVersion}; target BIOS {lookupResult.LatestVersionLabel}."
            : $"Current BIOS {firmware.BiosVersionLabel}; target version still needs explicit technician confirmation.";

        return new FirmwareSafetyGate(
            "Technician handoff",
            FirmwareSafetyGateSeverity.Info,
            "A human technician still owns the final flash authorization.",
            $"{targetClause} Keep the maintenance window, rollback plan, and release-note rationale explicit.",
            "Record the reason for the flash, confirm rollback or recovery options, and capture pre-flash evidence before proceeding.");
    }

    private static string ResolveSystemDrive()
    {
        string? root = Path.GetPathRoot(Environment.SystemDirectory);
        return string.IsNullOrWhiteSpace(root)
            ? "C:"
            : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsBitLockerStatusProbe : IBitLockerStatusProbe
{
    public Task<BitLockerVolumeStatus> GetSystemDriveStatusAsync(
        string systemDrive,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                try
                {
                    string escapedDrive = systemDrive.Replace("'", "''", StringComparison.Ordinal);
                    using ManagementObjectSearcher searcher = new(
                        @"root\CIMV2\Security\MicrosoftVolumeEncryption",
                        $"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = '{escapedDrive}'");

                    ManagementObject? volume = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (volume is null)
                    {
                        return new BitLockerVolumeStatus(
                            null,
                            "BitLocker status unavailable.",
                            $"Windows did not expose BitLocker telemetry for {systemDrive}.");
                    }

                    using ManagementBaseObject? outParams = volume.InvokeMethod("GetProtectionStatus", null, null);
                    bool? isProtected = NormalizeProtectionStatus(outParams?["ProtectionStatus"]);

                    return isProtected switch
                    {
                        true => new BitLockerVolumeStatus(
                            true,
                            $"BitLocker protection is active on {systemDrive}.",
                            "A firmware flash can trigger a recovery challenge if protection is not suspended first."),
                        false => new BitLockerVolumeStatus(
                            false,
                            $"BitLocker protection is not active on {systemDrive}.",
                            "Windows did not report an active BitLocker protection state for the system drive."),
                        _ => new BitLockerVolumeStatus(
                            null,
                            $"BitLocker protection could not be determined for {systemDrive}.",
                            "Windows returned the system drive, but the protection state was not conclusive.")
                    };
                }
                catch (Exception ex)
                {
                    return new BitLockerVolumeStatus(
                        null,
                        "BitLocker status unavailable.",
                        $"BitLocker inventory failed: {ex.Message}");
                }
            },
            cancellationToken);

    private static bool? NormalizeProtectionStatus(object? rawValue) => rawValue switch
    {
        1 or 1U or 1L => true,
        0 or 0U or 0L => false,
        string text when uint.TryParse(text, out uint parsed) => parsed switch
        {
            1U => true,
            0U => false,
            _ => null
        },
        _ => null
    };
}

[SupportedOSPlatform("windows")]
public sealed class WindowsPowerStatusProbe : IPowerStatusProbe
{
    public Task<SystemPowerSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                if (!NativeMethods.GetSystemPowerStatus(out SystemPowerStatus status))
                {
                    return new SystemPowerSnapshot(
                        null,
                        false,
                        null,
                        "Power status unavailable.",
                        "Windows did not return current power telemetry.");
                }

                bool hasBattery = status.BatteryFlag != NoBatteryFlag;
                bool? isAcOnline = status.ACLineStatus switch
                {
                    1 => true,
                    0 => false,
                    _ => null
                };

                int? batteryPercentage = status.BatteryLifePercent == byte.MaxValue
                    ? null
                    : status.BatteryLifePercent;

                return (isAcOnline, hasBattery) switch
                {
                    (true, true) => new SystemPowerSnapshot(
                        true,
                        true,
                        batteryPercentage,
                        batteryPercentage is int percent
                            ? $"AC power is connected. Battery is at {percent:N0}%."
                            : "AC power is connected.",
                        "The system is reporting external power, which is the minimum posture for a firmware flash."),
                    (true, false) => new SystemPowerSnapshot(
                        true,
                        false,
                        null,
                        "AC power is connected. No battery is present.",
                        "This looks like a desktop or a system without battery telemetry. Verify PSU or UPS stability manually."),
                    (false, true) => new SystemPowerSnapshot(
                        false,
                        true,
                        batteryPercentage,
                        batteryPercentage is int percent
                            ? $"System is running on battery at {percent:N0}%."
                            : "System is running on battery.",
                        "Firmware flashing should not start while the system is on battery power."),
                    (false, false) => new SystemPowerSnapshot(
                        false,
                        false,
                        null,
                        "AC status is not online and no battery telemetry is present.",
                        "Windows power telemetry is unusual for a flash window. Confirm stable external power manually."),
                    _ => new SystemPowerSnapshot(
                        null,
                        hasBattery,
                        batteryPercentage,
                        "Power status is inconclusive.",
                        "Windows did not expose a deterministic AC-line state for this machine.")
                };
            },
            cancellationToken);

    private const byte NoBatteryFlag = 128;

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);
    }
}
