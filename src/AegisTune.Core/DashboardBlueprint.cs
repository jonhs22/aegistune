namespace AegisTune.Core;

public static class DashboardBlueprint
{
    public static DashboardSnapshot Create(
        SystemProfile profile,
        FirmwareInventorySnapshot firmware,
        WindowsHealthSnapshot health,
        AudioInventorySnapshot audio,
        AppSettings settings,
        CleanupScanResult cleanupScan,
        DeviceInventorySnapshot deviceInventory,
        StartupInventorySnapshot startupInventory,
        AppInventorySnapshot appInventory,
        RepairScanResult repairScan,
        int reportCount,
        DateTimeOffset now)
    {
        var modules = new[]
        {
            new ModuleSnapshot(
                AppSection.Health,
                "Windows Health",
                "Review crashes, Windows Update issues, services, and scheduled tasks.",
                RiskLevel.Review,
                ModuleReadiness.Operational,
                health.IssueCount,
                health.IssueCount == 1 ? "1 item" : $"{health.IssueCount:N0} items",
                "Health items to review",
                BuildHealthStatusLine(health),
                false,
                new[]
                {
                    new ModuleAction("Review crash evidence", "Check recent application crash signals and decide whether they are still active."),
                    new ModuleAction("Review services and tasks", "Open only the broken or suspicious background items that need attention.")
                }),
            new ModuleSnapshot(
                AppSection.Audio,
                "Audio & Sound",
                "Review playback, recording, mute, and endpoint volume posture.",
                RiskLevel.Review,
                ModuleReadiness.Operational,
                audio.IssueCount,
                $"{audio.OutputDeviceCount + audio.InputDeviceCount:N0} endpoints",
                "Audio endpoints found",
                BuildAudioStatusLine(audio),
                false,
                new[]
                {
                    new ModuleAction("Review playback defaults", "Check whether the default output device is muted or too low for the current session."),
                    new ModuleAction("Review microphone defaults", "Check whether the default recording device is muted, too low, or missing from the current Windows route."),
                    new ModuleAction("Open Windows sound tools", "Use Sound settings, Volume mixer, or the classic sound panel for routing changes.")
                }),
            new ModuleSnapshot(
                AppSection.Cleaner,
                "Cleanup",
                "Review safe temp, cache, recycle bin, and browser trace cleanup.",
                RiskLevel.Safe,
                ModuleReadiness.Operational,
                cleanupScan.ActionableTargetCount,
                cleanupScan.TotalBytesLabel,
                "Potential reclaim",
                BuildCleanerStatusLine(settings, cleanupScan),
                false,
                new[]
                {
                    new ModuleAction("Review safe cleanup", "Inspect temp files, recycle bin, and log folders before anything is deleted."),
                    new ModuleAction("Configure exclusions", "Keep folders or patterns out of future cleanup plans.")
                }),
            new ModuleSnapshot(
                AppSection.Drivers,
                "Drivers & Firmware",
                "Review device risk, official BIOS status, and supported update routes.",
                RiskLevel.Review,
                ModuleReadiness.Preview,
                deviceInventory.NeedsAttentionCount,
                $"{deviceInventory.TotalDeviceCount:N0} devices",
                "Devices audited",
                BuildDriverStatusLine(deviceInventory, firmware),
                true,
                new[]
                {
                    new ModuleAction("Scan devices and driver status", "Refresh device and driver metadata before choosing a source."),
                    new ModuleAction("Review firmware route", "Keep BIOS updates on an official OEM or board-vendor path with model-specific evidence."),
                    new ModuleAction("Check official BIOS version", "Verify the latest BIOS only where the vendor exposes a deterministic official source. Otherwise stay on the official support or vendor-tool path."),
                    new ModuleAction("Install selected local driver", "Use only a vetted local INF match or stay on Windows Update or the official OEM path.")
                }),
            new ModuleSnapshot(
                AppSection.Startup,
                "Startup Review",
                "Disable startup items, review impact, and clean up broken launch entries.",
                RiskLevel.Safe,
                ModuleReadiness.Operational,
                startupInventory.ActionableCount,
                $"{startupInventory.EntryCount:N0} entries",
                "Startup entries found",
                BuildStartupStatusLine(startupInventory),
                false,
                new[]
                {
                    new ModuleAction("Scan startup items", "See which apps affect login time before disabling anything."),
                    new ModuleAction("Disable startup launch", "Remove an app from Windows startup without uninstalling the app itself."),
                    new ModuleAction("Remove broken entries", "Delete orphaned startup registrations only when the source and missing target are both verified.")
                }),
            new ModuleSnapshot(
                AppSection.Apps,
                "Apps & Uninstall",
                "Find broken installs, uninstall leftovers, and app-linked evidence.",
                RiskLevel.Review,
                ModuleReadiness.Operational,
                appInventory.BrokenInstallEvidenceCount,
                $"{appInventory.ApplicationCount:N0} apps",
                "Applications inventoried",
                BuildAppsStatusLine(appInventory),
                false,
                new[]
                {
                    new ModuleAction("Review app issues", "See broken install evidence, uninstall targets, and install paths in one place."),
                    new ModuleAction("Review leftovers", "Prepare uninstall residue analysis before removing anything.")
                }),
            new ModuleSnapshot(
                AppSection.Repair,
                "Repair & Recovery",
                "Use official repair routes and fix broken references safely.",
                RiskLevel.Review,
                ModuleReadiness.Preview,
                repairScan.CandidateCount,
                $"{repairScan.CandidateCount:N0} candidates",
                "Repair candidates",
                BuildRepairStatusLine(repairScan),
                repairScan.CandidateCount > 0,
                new[]
                {
                    new ModuleAction("Review shell remnants", "Surface broken context-menu and uninstall traces with proof."),
                    new ModuleAction("Review dependency advice", "Escalate missing DLL and runtime cases to Microsoft or the official vendor only."),
                    new ModuleAction("Run registry repair packs", "Back up stale keys, require restore point preflight, and apply only targeted registry fixes.")
                }),
            new ModuleSnapshot(
                AppSection.Reports,
                "Reports",
                "Action logs, before-and-after evidence, and export-friendly trust surfaces.",
                RiskLevel.Safe,
                ModuleReadiness.Operational,
                0,
                reportCount == 1 ? "1 report" : $"{reportCount:N0} reports",
                "Persisted snapshots",
                BuildReportsStatusLine(reportCount),
                false,
                new[]
                {
                    new ModuleAction("View action log", "Track what the app scanned, suggested, and changed."),
                    new ModuleAction("Export report files", "Write the current maintenance report to JSON and Markdown for support or technician review.")
                })
        };

        var activities = new[]
        {
            new RecentActivity(now.AddMinutes(-3), "Windows health refreshed", BuildHealthActivity(health)),
            new RecentActivity(now.AddMinutes(-7), "Audio posture refreshed", BuildAudioActivity(audio)),
            new RecentActivity(now.AddMinutes(-11), "Cleanup scan refreshed", BuildCleanupActivity(cleanupScan)),
            new RecentActivity(now.AddMinutes(-15), "Firmware posture refreshed", BuildFirmwareActivity(firmware)),
            new RecentActivity(now.AddMinutes(-19), "Startup and repair refresh", BuildRepairActivity(startupInventory, repairScan)),
            new RecentActivity(now.AddMinutes(-23), "Inventory refresh", BuildInventoryActivity(deviceInventory, appInventory))
        };

        return new DashboardSnapshot(profile, firmware, settings, modules, activities);
    }

    private static string BuildCleanerStatusLine(AppSettings settings, CleanupScanResult cleanupScan)
    {
        if (!string.IsNullOrWhiteSpace(cleanupScan.WarningMessage))
        {
            return cleanupScan.WarningMessage;
        }

        if (cleanupScan.ActionableTargetCount == 0)
        {
            return settings.DryRunEnabled
                ? "Dry-run mode is active. No reclaimable temp content was found in the current scan."
                : "Execution is enabled after explicit confirmation. No reclaimable temp content was found in the current scan.";
        }

        string prefix = settings.DryRunEnabled
            ? "Dry-run mode is active."
            : "Execution is enabled after explicit confirmation.";

        return $"{prefix} {cleanupScan.ActionableTargetCount} cleanup target(s) currently show {cleanupScan.TotalBytesLabel} across {cleanupScan.TotalFileCountLabel}.";
    }

    private static string BuildHealthStatusLine(WindowsHealthSnapshot health)
    {
        if (!string.IsNullOrWhiteSpace(health.WarningMessage))
        {
            return health.WarningMessage!;
        }

        if (health.IssueCount == 0)
        {
            return "No issues are currently flagged within the active Windows health review scope.";
        }

        return $"{health.CrashCount:N0} crash signal(s), {health.WindowsUpdateIssueCount:N0} Windows Update issue(s), {health.ServiceReviewCount:N0} service candidate(s), and {health.ScheduledTaskReviewCount:N0} scheduled task candidate(s) need review.";
    }

    private static string BuildAudioStatusLine(AudioInventorySnapshot audio)
    {
        if (!string.IsNullOrWhiteSpace(audio.WarningMessage))
        {
            return audio.WarningMessage!;
        }

        if (audio.OutputDeviceCount == 0 && audio.InputDeviceCount == 0)
        {
            return "No active playback or recording devices are currently exposed by Windows. Open Sound settings and confirm that Windows still sees your audio hardware.";
        }

        if (audio.IssueCount == 0)
        {
            return $"Default playback and recording devices are available. {audio.MutedEndpointCount:N0} endpoint(s) are muted and {audio.LowVolumeEndpointCount:N0} endpoint(s) sit below the recommended {audio.RecommendedVolumePercent:N0}% review floor.";
        }

        return $"{audio.IssueCount:N0} default audio item(s) need review. {audio.MutedEndpointCount:N0} endpoint(s) are muted and {audio.LowVolumeEndpointCount:N0} endpoint(s) are below the recommended {audio.RecommendedVolumePercent:N0}% target.";
    }

    private static string BuildDriverStatusLine(DeviceInventorySnapshot deviceInventory, FirmwareInventorySnapshot firmware)
    {
        string firmwareClause = string.IsNullOrWhiteSpace(firmware.WarningMessage)
            ? $" Firmware route: {firmware.SupportIdentityLabel} reports BIOS {firmware.BiosVersionLabel} ({firmware.BiosReleaseDateLabel}) and should stay on the {firmware.SupportRouteLabel.ToLowerInvariant()}."
            : $" Firmware warning: {firmware.WarningMessage}";

        if (!string.IsNullOrWhiteSpace(deviceInventory.WarningMessage))
        {
            return $"{deviceInventory.WarningMessage}{firmwareClause}";
        }

        if (deviceInventory.NeedsAttentionCount == 0)
        {
            return $"Driver inventory completed. {deviceInventory.TotalDeviceCount:N0} devices were enumerated and none currently require review.{firmwareClause}";
        }

        return $"{deviceInventory.NeedsAttentionCount:N0} device(s) need review. {deviceInventory.PriorityReviewCount:N0} are priority cases, {deviceInventory.CompatibleFallbackReviewCount:N0} rely on compatible-ID fallback, and {deviceInventory.GenericProviderReviewCount:N0} still use a Microsoft-supplied driver in a critical class.{firmwareClause}";
    }

    private static string BuildStartupStatusLine(StartupInventorySnapshot startupInventory)
    {
        if (!string.IsNullOrWhiteSpace(startupInventory.WarningMessage))
        {
            return startupInventory.WarningMessage;
        }

        if (startupInventory.ActionableCount == 0)
        {
            return $"Startup inventory enumerated {startupInventory.EntryCount:N0} entries and found no orphaned or high-impact items.";
        }

        return $"Startup inventory found {startupInventory.EntryCount:N0} entries, {startupInventory.OrphanedCount:N0} orphaned item(s), and {startupInventory.HighImpactCount:N0} high-impact launch item(s).";
    }

    private static string BuildCleanupActivity(CleanupScanResult cleanupScan) =>
        cleanupScan.ActionableTargetCount == 0
            ? "No reclaimable temp content was detected across the current cleanup targets."
            : $"{cleanupScan.ActionableTargetCount} target(s) currently expose {cleanupScan.TotalBytesLabel} across {cleanupScan.TotalFileCountLabel}.";

    private static string BuildAppsStatusLine(AppInventorySnapshot appInventory)
    {
        if (!string.IsNullOrWhiteSpace(appInventory.WarningMessage))
        {
            return appInventory.WarningMessage;
        }

        return $"{appInventory.ApplicationCount:N0} apps were inventoried across desktop and packaged sources. {appInventory.LeftoverReviewCandidateCount:N0} uninstall leftover candidate(s) need review.";
    }

    private static string BuildRepairStatusLine(RepairScanResult repairScan)
    {
        if (!string.IsNullOrWhiteSpace(repairScan.WarningMessage))
        {
            return repairScan.WarningMessage;
        }

        return repairScan.CandidateCount == 0
            ? "Repair scan found no clear orphaned startup, uninstall residue, registry repair, or dependency repair candidates. Safety history and rollback files remain available in Safety & Undo."
            : $"{repairScan.CandidateCount:N0} evidence-backed repair candidate(s) are ready for review, including targeted registry repair packs and official runtime guidance when Windows exposes missing-DLL evidence. Safety history and rollback files remain available in Safety & Undo.";
    }

    private static string BuildReportsStatusLine(int reportCount) =>
        reportCount == 0
            ? "No persisted report snapshots exist yet for this device."
            : $"{reportCount:N0} persisted report snapshot(s) are available in the local store.";

    private static string BuildRepairActivity(StartupInventorySnapshot startupInventory, RepairScanResult repairScan) =>
        $"{startupInventory.EntryCount:N0} startup entries were checked and produced {repairScan.CandidateCount:N0} repair candidate(s).";

    private static string BuildHealthActivity(WindowsHealthSnapshot health) =>
        health.IssueCount == 0
            ? "No issues were flagged within the active Windows health review scope."
            : $"{health.IssueCount:N0} health item(s) were flagged across crashes, Windows Update, services, and scheduled tasks.";

    private static string BuildAudioActivity(AudioInventorySnapshot audio) =>
        audio.OutputDeviceCount + audio.InputDeviceCount == 0
            ? "No active playback or recording endpoints were exposed by Windows."
            : $"{audio.OutputDeviceCount + audio.InputDeviceCount:N0} audio endpoint(s) were reviewed. {audio.IssueCount:N0} default audio item(s) need attention.";

    private static string BuildFirmwareActivity(FirmwareInventorySnapshot firmware) =>
        $"{firmware.SupportIdentityLabel} reports BIOS {firmware.BiosVersionLabel} from {firmware.BiosReleaseDateLabel}. {firmware.SupportIdentitySourceLabel}.";

    private static string BuildInventoryActivity(DeviceInventorySnapshot deviceInventory, AppInventorySnapshot appInventory) =>
        $"{deviceInventory.TotalDeviceCount:N0} devices and {appInventory.ApplicationCount:N0} installed apps were inventoried for this session.";
}
