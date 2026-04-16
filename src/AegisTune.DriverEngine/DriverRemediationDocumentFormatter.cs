using System.Text;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public static class DriverRemediationDocumentFormatter
{
    public static string BuildClipboardText(DriverDeviceRecord device, DriverRemediationPlan plan) =>
        BuildMarkdown(device, plan);

    public static string BuildMarkdown(DriverDeviceRecord device, DriverRemediationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(plan);

        StringBuilder builder = new();
        builder.AppendLine("# AegisTune Driver Remediation Plan");
        builder.AppendLine();
        builder.AppendLine("- Publisher: ichiphost");
        builder.AppendLine("- Support: info@ichiphost.gr");
        builder.AppendLine("- Creator: John Papadakis");
        builder.AppendLine($"- Device: {device.FriendlyName}");
        builder.AppendLine($"- Review bucket: {device.ReviewBucketLabel}");
        builder.AppendLine($"- Review category: {device.ReviewCategory}");
        builder.AppendLine($"- Source path: {plan.SourceLabel}");
        builder.AppendLine($"- Reboot guidance: {plan.RebootGuidanceLabel}");
        builder.AppendLine();
        builder.AppendLine("## Plan summary");
        builder.AppendLine();
        builder.AppendLine(plan.Summary);
        builder.AppendLine();
        builder.AppendLine("## Source recommendation");
        builder.AppendLine();
        builder.AppendLine($"- {plan.SourceLabel}");
        builder.AppendLine($"- {plan.SourceReason}");
        builder.AppendLine();
        builder.AppendLine("## Rollback readiness");
        builder.AppendLine();
        builder.AppendLine($"- {plan.RollbackLabel}");
        builder.AppendLine($"- {plan.RollbackDetail}");
        builder.AppendLine();
        builder.AppendLine("## Device evidence");
        builder.AppendLine();
        builder.AppendLine($"- Class: {device.DeviceClass}");
        builder.AppendLine($"- Manufacturer: {device.Manufacturer}");
        builder.AppendLine($"- Provider: {device.ProviderLabel}");
        builder.AppendLine($"- Version: {device.VersionLabel}");
        builder.AppendLine($"- Driver date: {device.DriverDateLabel}");
        builder.AppendLine($"- Signing: {device.SigningLabel}");
        builder.AppendLine($"- Signer: {device.SignerLabel}");
        builder.AppendLine($"- Service: {device.ServiceLabel}");
        builder.AppendLine($"- INF: {device.InfLabel}");
        builder.AppendLine($"- Instance ID: {device.InstanceId}");
        builder.AppendLine($"- Evidence tier: {device.EvidenceTierLabel}");
        builder.AppendLine($"- Match confidence: {device.MatchConfidenceLabel}");
        builder.AppendLine($"- Match reason: {device.MatchConfidenceReason}");
        builder.AppendLine();
        builder.AppendLine("## Verification checklist");
        builder.AppendLine();

        foreach (DriverVerificationStep step in plan.VerificationSteps)
        {
            builder.AppendLine($"### {step.Title}");
            builder.AppendLine(step.Detail);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}
