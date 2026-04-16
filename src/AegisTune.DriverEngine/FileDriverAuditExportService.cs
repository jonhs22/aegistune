using System.Text;
using System.Text.Json;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class FileDriverAuditExportService : IDriverAuditExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _exportRoot;

    public FileDriverAuditExportService(string? exportRoot = null)
    {
        _exportRoot = exportRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisTune",
                "DriverAudits");
    }

    public async Task<DriverAuditExportResult> ExportAsync(
        DeviceInventorySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Directory.CreateDirectory(_exportRoot);

        string exportDirectory = Path.Combine(_exportRoot, $"driver-audit-{snapshot.ScannedAt:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(exportDirectory);

        string jsonPath = Path.Combine(exportDirectory, "driver-audit.json");
        string markdownPath = Path.Combine(exportDirectory, "driver-audit.md");
        string handoffPath = Path.Combine(exportDirectory, "priority-driver-handoff.md");
        string remediationBundlePath = Path.Combine(exportDirectory, "priority-driver-remediation-bundle.md");
        string remediationPlansDirectory = Path.Combine(exportDirectory, "remediation-plans");

        Directory.CreateDirectory(remediationPlansDirectory);

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(snapshot, SerializerOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            BuildMarkdown(snapshot),
            cancellationToken);
        await File.WriteAllTextAsync(
            handoffPath,
            BuildPriorityHandoff(snapshot),
            cancellationToken);
        await File.WriteAllTextAsync(
            remediationBundlePath,
            await BuildRemediationBundleAsync(snapshot, remediationPlansDirectory, cancellationToken),
            cancellationToken);

        return new DriverAuditExportResult(
            DateTimeOffset.Now,
            exportDirectory,
            jsonPath,
            markdownPath,
            handoffPath,
            remediationBundlePath,
            remediationPlansDirectory);
    }

    private static string BuildMarkdown(DeviceInventorySnapshot snapshot)
    {
        StringBuilder builder = new();
        builder.AppendLine("# AegisTune Driver Audit");
        builder.AppendLine();
        builder.AppendLine($"- Exported: {snapshot.ScannedAt.ToLocalTime():f}");
        builder.AppendLine($"- Devices audited: {snapshot.TotalDeviceCount:N0}");
        builder.AppendLine($"- Priority review: {snapshot.PriorityReviewCount:N0}");
        builder.AppendLine($"- Advisory review: {snapshot.AdvisoryReviewCount:N0}");
        builder.AppendLine($"- Unsigned drivers: {snapshot.UnsignedDriverCount:N0}");
        builder.AppendLine($"- Critical classes: {snapshot.CriticalClassCount:N0}");
        builder.AppendLine($"- High confidence OEM matches: {snapshot.HighConfidenceMatchCount:N0}");
        builder.AppendLine($"- Medium confidence OEM matches: {snapshot.MediumConfidenceMatchCount:N0}");
        builder.AppendLine($"- Low confidence OEM matches: {snapshot.LowConfidenceMatchCount:N0}");
        builder.AppendLine($"- Unknown OEM matches: {snapshot.UnknownConfidenceMatchCount:N0}");
        builder.AppendLine($"- Hardware-backed review cases: {snapshot.HardwareBackedReviewCount:N0}");
        builder.AppendLine($"- Compatible-fallback review cases: {snapshot.CompatibleFallbackReviewCount:N0}");
        builder.AppendLine($"- No-identifier review cases: {snapshot.NoIdentifierReviewCount:N0}");
        builder.AppendLine();
        builder.AppendLine("## Priority and review queue");
        builder.AppendLine();

        foreach (DriverDeviceRecord device in snapshot.Devices.Where(device => device.NeedsAttention).Take(20))
        {
            builder.AppendLine($"### {device.FriendlyName}");
            builder.AppendLine($"- Review bucket: {device.ReviewBucketLabel}");
            builder.AppendLine($"- Category: {device.ReviewCategory}");
            builder.AppendLine($"- Class: {device.DeviceClass}");
            builder.AppendLine($"- Provider: {device.ProviderLabel}");
            builder.AppendLine($"- Version: {device.VersionLabel}");
            builder.AppendLine($"- Signed: {device.SigningLabel}");
            builder.AppendLine($"- Service: {device.ServiceLabel}");
            builder.AppendLine($"- Presence: {device.PresenceLabel}");
            builder.AppendLine($"- Class GUID: {device.ClassGuidLabel}");
            builder.AppendLine($"- Match confidence: {device.MatchConfidenceLabel}");
            builder.AppendLine($"- Match reason: {device.MatchConfidenceReason}");
            builder.AppendLine($"- Evidence tier: {device.EvidenceTierLabel}");
            builder.AppendLine($"- Match evidence source: {device.MatchEvidenceSourceLabel}");
            builder.AppendLine($"- Provider/manufacturer aligned: {(device.ProviderManufacturerAligned ? "Yes" : "No")}");
            builder.AppendLine($"- Generic provider signal: {(device.ProviderLooksGeneric ? "Yes" : "No")}");
            builder.AppendLine($"- Subsystem-specific hardware ID: {(device.HasSubsystemSpecificHardwareId ? "Yes" : "No")}");
            builder.AppendLine($"- Driver date: {device.DriverDateLabel}");
            builder.AppendLine($"- Hardware IDs: {device.HardwareIdCountLabel}");
            if (device.HasHardwareIds)
            {
                foreach (string hardwareId in device.HardwareIds!.Take(4))
                {
                    builder.AppendLine($"  - {hardwareId}");
                }
            }
            builder.AppendLine($"- Compatible IDs: {device.CompatibleIdCountLabel}");
            if (device.HasCompatibleIds)
            {
                foreach (string compatibleId in device.CompatibleIds!.Take(4))
                {
                    builder.AppendLine($"  - {compatibleId}");
                }
            }
            builder.AppendLine($"- INF: {device.InfLabel}");
            builder.AppendLine($"- Instance ID: {device.InstanceId}");
            builder.AppendLine($"- Safe source path: {device.SafeSourcePath}");
            builder.AppendLine($"- Recommended action: {device.RecommendedAction}");
            DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);
            builder.AppendLine($"- Remediation source: {plan.SourceLabel}");
            builder.AppendLine($"- Reboot guidance: {plan.RebootGuidanceLabel}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildPriorityHandoff(DeviceInventorySnapshot snapshot)
    {
        StringBuilder builder = new();
        builder.AppendLine("# AegisTune Priority Driver Handoff");
        builder.AppendLine();
        builder.AppendLine($"Generated: {snapshot.ScannedAt.ToLocalTime():f}");
        builder.AppendLine();

        DriverDeviceRecord[] handoffDevices = snapshot.Devices
            .Where(device => device.RequiresPriorityReview)
            .Take(15)
            .ToArray();

        if (handoffDevices.Length == 0)
        {
            builder.AppendLine("No priority-review devices were found in this scan.");
            return builder.ToString();
        }

        foreach (DriverDeviceRecord device in handoffDevices)
        {
            builder.AppendLine($"## {device.FriendlyName}");
            builder.AppendLine();
            foreach (string line in device.TechnicianHandoffSummary.Split(Environment.NewLine, StringSplitOptions.None))
            {
                builder.AppendLine($"- {line}");
            }

            if (device.HasHardwareIds)
            {
                builder.AppendLine("- Hardware IDs:");
                foreach (string hardwareId in device.HardwareIds!.Take(8))
                {
                    builder.AppendLine($"  - {hardwareId}");
                }
            }

            builder.AppendLine($"- Match confidence label: {device.MatchConfidenceLabel}");
            builder.AppendLine($"- Match confidence reason: {device.MatchConfidenceReason}");
            builder.AppendLine($"- Evidence tier: {device.EvidenceTierLabel}");
            builder.AppendLine($"- Match evidence source: {device.MatchEvidenceSourceLabel}");
            builder.AppendLine($"- Provider/manufacturer aligned: {(device.ProviderManufacturerAligned ? "Yes" : "No")}");
            builder.AppendLine($"- Subsystem-specific hardware ID: {(device.HasSubsystemSpecificHardwareId ? "Yes" : "No")}");
            DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);
            builder.AppendLine($"- Remediation source: {plan.SourceLabel}");
            builder.AppendLine($"- Reboot guidance: {plan.RebootGuidanceLabel}");
            builder.AppendLine($"- Rollback prep: {plan.RollbackLabel}");
            builder.AppendLine($"- Verification status: {plan.VerificationStatusLine}");
            if (!device.HasHardwareIds && device.HasCompatibleIds)
            {
                builder.AppendLine("- Compatible ID fallback evidence:");
                foreach (string compatibleId in device.CompatibleIds!.Take(4))
                {
                    builder.AppendLine($"  - {compatibleId}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static async Task<string> BuildRemediationBundleAsync(
        DeviceInventorySnapshot snapshot,
        string remediationPlansDirectory,
        CancellationToken cancellationToken)
    {
        StringBuilder builder = new();
        builder.AppendLine("# AegisTune Priority Driver Remediation Bundle");
        builder.AppendLine();
        builder.AppendLine($"Generated: {snapshot.ScannedAt.ToLocalTime():f}");
        builder.AppendLine();

        DriverDeviceRecord[] remediationDevices = snapshot.Devices
            .Where(device => device.RequiresPriorityReview)
            .Take(15)
            .ToArray();

        if (remediationDevices.Length == 0)
        {
            builder.AppendLine("No priority-review devices were found in this scan.");
            return builder.ToString();
        }

        foreach (DriverDeviceRecord device in remediationDevices)
        {
            DriverRemediationPlan plan = DriverRemediationPlanner.Build(device);
            string fileName = $"{SanitizePathPart(device.FriendlyName)}-remediation-plan.md";
            string filePath = Path.Combine(remediationPlansDirectory, fileName);
            await File.WriteAllTextAsync(
                filePath,
                DriverRemediationDocumentFormatter.BuildMarkdown(device, plan),
                cancellationToken);

            builder.AppendLine($"## {device.FriendlyName}");
            builder.AppendLine();
            builder.AppendLine($"- Review bucket: {device.ReviewBucketLabel}");
            builder.AppendLine($"- Review category: {device.ReviewCategory}");
            builder.AppendLine($"- Remediation source: {plan.SourceLabel}");
            builder.AppendLine($"- Why this path: {plan.SourceReason}");
            builder.AppendLine($"- Reboot guidance: {plan.RebootGuidanceLabel}");
            builder.AppendLine($"- Rollback prep: {plan.RollbackLabel}");
            builder.AppendLine($"- Plan file: {fileName}");
            builder.AppendLine();
            builder.AppendLine("### Verification checklist");
            builder.AppendLine();

            foreach (DriverVerificationStep step in plan.VerificationSteps)
            {
                builder.AppendLine($"- {step.Title}: {step.Detail}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
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
