namespace AegisTune.Core;

public sealed record FirmwareFlashPreparationGuide(
    string TargetSummary,
    string ReleaseNotesSummary,
    string ReleaseNotesPreview,
    string CommandPreview,
    string ChecklistPreview,
    string? ReleaseNotesUrl = null)
{
    public bool HasReleaseNotesUrl => !string.IsNullOrWhiteSpace(ReleaseNotesUrl);

    public string ReleaseNotesUrlLabel => HasReleaseNotesUrl
        ? ReleaseNotesUrl!
        : "No official release-notes URL is cached for this lookup yet.";

    public string ClipboardText =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"Target summary: {TargetSummary}",
                $"Release-note route: {ReleaseNotesSummary}",
                $"Release-note preview: {ReleaseNotesPreview}",
                "Command kit:",
                CommandPreview,
                "Checklist:",
                ChecklistPreview
            });
}

public static class FirmwareFlashPreparationAdvisor
{
    public static FirmwareFlashPreparationGuide Build(
        FirmwareInventorySnapshot? firmware,
        FirmwareReleaseLookupResult? lookupResult,
        FirmwareSafetyAssessment? assessment)
    {
        string systemDrive = string.IsNullOrWhiteSpace(assessment?.SystemDrive)
            ? "C:"
            : assessment!.SystemDrive;
        string currentVersion = lookupResult?.CurrentVersion
            ?? firmware?.BiosVersionLabel
            ?? "Unknown BIOS version";
        bool latestMatchesCurrent = lookupResult?.HasLatestRelease == true
            && FirmwareVersionComparer.AreEquivalent(currentVersion, lookupResult.LatestVersion);

        string targetSummary = BuildTargetSummary(currentVersion, lookupResult, latestMatchesCurrent);
        string releaseNotesSummary = BuildReleaseNotesSummary(lookupResult);
        string releaseNotesPreview = BuildReleaseNotesPreview(lookupResult);
        string commandPreview = BuildCommandPreview(systemDrive, assessment);
        string checklistPreview = BuildChecklistPreview(systemDrive, lookupResult, assessment, latestMatchesCurrent);

        return new FirmwareFlashPreparationGuide(
            targetSummary,
            releaseNotesSummary,
            releaseNotesPreview,
            commandPreview,
            checklistPreview,
            lookupResult?.DetailsUrl);
    }

    private static string BuildTargetSummary(
        string currentVersion,
        FirmwareReleaseLookupResult? lookupResult,
        bool latestMatchesCurrent)
    {
        if (lookupResult?.HasLatestRelease != true)
        {
            return $"Current BIOS {currentVersion}; no deterministic target is cached yet. Record the exact BIOS package manually from the official vendor workflow before any flash.";
        }

        string targetLabel = string.Equals(lookupResult.LatestReleaseTitleLabel, lookupResult.LatestVersionLabel, StringComparison.Ordinal)
            ? lookupResult.LatestVersionLabel
            : $"{lookupResult.LatestReleaseTitleLabel} [{lookupResult.LatestVersionLabel}]";
        string betaClause = lookupResult.LatestIsBeta
            ? " The mapped target is marked Beta and should stay on a technician-only path."
            : string.Empty;

        return latestMatchesCurrent
            ? $"Current BIOS {currentVersion} already matches the latest official target {targetLabel}. Only proceed if the vendor documents a recovery or issue-remediation reason.{betaClause}"
            : $"Current BIOS {currentVersion}; staged target {targetLabel}.{betaClause}";
    }

    private static string BuildReleaseNotesSummary(FirmwareReleaseLookupResult? lookupResult)
    {
        if (lookupResult?.HasDetailsUrl == true)
        {
            string targetLabel = string.Equals(lookupResult.LatestReleaseTitleLabel, lookupResult.LatestVersionLabel, StringComparison.Ordinal)
                ? lookupResult.LatestVersionLabel
                : lookupResult.LatestReleaseTitleLabel;
            return $"Official release details are mapped for {targetLabel}. Review the vendor changelog before the flash window.";
        }

        if (lookupResult?.HasSupportUrl == true)
        {
            return "No direct release-details URL is cached yet. Stay on the official support route and capture the exact BIOS package notes before any flash.";
        }

        return "No official release-details route is cached yet. Keep the BIOS decision on a manual vendor-review path.";
    }

    private static string BuildReleaseNotesPreview(FirmwareReleaseLookupResult? lookupResult)
    {
        if (!string.IsNullOrWhiteSpace(lookupResult?.LatestReleaseNotesSummary))
        {
            return lookupResult.LatestReleaseNotesSummaryLabel;
        }

        return lookupResult?.HasDetailsUrl == true
            ? "No release-note preview was cached from the lookup response. Open the vendor details page and compare the changelog manually."
            : "No release-note preview is available yet.";
    }

    private static string BuildCommandPreview(string systemDrive, FirmwareSafetyAssessment? assessment)
    {
        string normalizedDrive = NormalizeDrive(systemDrive);

        return assessment?.RequiresBitLockerSuspension == true
            ? string.Join(
                Environment.NewLine,
                new[]
                {
                    "PowerShell (elevated):",
                    $"Get-BitLockerVolume -MountPoint '{normalizedDrive}'",
                    $"Suspend-BitLocker -MountPoint '{normalizedDrive}' -RebootCount 0",
                    $"Resume-BitLocker -MountPoint '{normalizedDrive}'",
                    "",
                    "Command Prompt verification:",
                    $"manage-bde -status {normalizedDrive}"
                })
            : string.Join(
                Environment.NewLine,
                new[]
                {
                    "PowerShell (elevated):",
                    $"Get-BitLockerVolume -MountPoint '{normalizedDrive}'",
                    "",
                    "Command Prompt verification:",
                    $"manage-bde -status {normalizedDrive}"
                });
    }

    private static string BuildChecklistPreview(
        string systemDrive,
        FirmwareReleaseLookupResult? lookupResult,
        FirmwareSafetyAssessment? assessment,
        bool latestMatchesCurrent)
    {
        string normalizedDrive = NormalizeDrive(systemDrive);
        string targetLine = lookupResult?.HasLatestRelease == true
            ? latestMatchesCurrent
                ? "Do not stage a BIOS flash unless you have a documented recovery or vendor-directed remediation reason."
                : $"Confirm {lookupResult.LatestVersionLabel} against the exact machine identity before staging the flash."
            : "Record the exact BIOS package manually from the official vendor workflow before staging any flash.";
        string releaseNotesLine = lookupResult?.HasDetailsUrl == true
            ? "Review the official release notes or changelog and compare them against the current BIOS before the maintenance window."
            : "Capture the vendor release notes or package description manually before you authorize any flash.";
        string bitLockerLine = assessment?.RequiresBitLockerSuspension == true
            ? $"Suspend BitLocker on {normalizedDrive}, confirm the recovery key is available, and resume protection after a healthy post-flash boot."
            : $"Verify BitLocker posture on {normalizedDrive} before the firmware window, even if Windows currently reports protection as off or unavailable.";
        string powerLine = assessment?.IsAcOnline switch
        {
            true => assessment.HasBattery
                ? "Keep AC power connected for the full flash and reboot sequence. Do not rely on battery alone during the maintenance window."
                : "Keep stable AC power or UPS coverage through the entire flash and reboot sequence.",
            false when assessment.HasBattery => "Connect AC power now. Do not flash while the machine is on battery.",
            false => "Confirm stable PSU or UPS coverage manually before any firmware flash.",
            _ => "Confirm stable AC power manually before any firmware flash."
        };

        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"1. {targetLine}",
                $"2. {releaseNotesLine}",
                $"3. {bitLockerLine}",
                $"4. {powerLine}",
                "5. Record the maintenance window, technician authorization, rollback or recovery path, and post-flash verification plan."
            });
    }

    private static string NormalizeDrive(string systemDrive) =>
        string.IsNullOrWhiteSpace(systemDrive)
            ? "C:"
            : systemDrive.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
