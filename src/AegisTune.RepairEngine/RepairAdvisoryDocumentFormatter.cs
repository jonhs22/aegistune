using System.Text;
using AegisTune.Core;

namespace AegisTune.RepairEngine;

public static class RepairAdvisoryDocumentFormatter
{
    public static string BuildClipboardText(RepairAdvisoryExportRequest advisory) => BuildMarkdown(advisory);

    public static string BuildMarkdown(RepairAdvisoryExportRequest advisory)
    {
        ArgumentNullException.ThrowIfNull(advisory);

        StringBuilder builder = new();
        builder.AppendLine("# AegisTune Repair Advisory");
        builder.AppendLine();
        builder.AppendLine("- Publisher: ichiphost");
        builder.AppendLine("- Support: info@ichiphost.gr");
        builder.AppendLine("- Creator: John Papadakis");
        builder.AppendLine($"- Advisory scope: {advisory.AdvisoryScope}");
        builder.AppendLine($"- Observed: {advisory.ObservedAt.ToLocalTime():f}");
        builder.AppendLine($"- Candidate count: {advisory.CandidateCount:N0}");
        builder.AppendLine($"- Status: {advisory.StatusLine}");
        builder.AppendLine();
        builder.AppendLine("## Advisory candidates");
        builder.AppendLine();

        if (advisory.Candidates.Count == 0)
        {
            builder.AppendLine("No repair advisory candidates were available for this export.");
            builder.AppendLine();
        }
        else
        {
            foreach (RepairCandidateRecord candidate in advisory.Candidates)
            {
                builder.AppendLine($"### {candidate.Title}");
                builder.AppendLine($"- Category: {candidate.Category}");
                builder.AppendLine($"- Risk: {candidate.RiskLabel}");
                builder.AppendLine($"- Elevation: {candidate.AdminRequirementLabel}");
                builder.AppendLine($"- Evidence: {candidate.EvidenceSummary}");
                builder.AppendLine($"- Proposed action: {candidate.ProposedAction}");
                builder.AppendLine($"- Source: {candidate.SourceLocation}");
                if (candidate.HasRelatedApplication)
                {
                    builder.AppendLine($"- Matched app: {candidate.RelatedApplicationLabel}");
                }

                if (candidate.HasApplicationPath)
                {
                    builder.AppendLine($"- App path: {candidate.ApplicationPathLabel}");
                }

                if (candidate.HasInstallLocation)
                {
                    builder.AppendLine($"- Install location: {candidate.InstallLocationLabel}");
                }

                if (candidate.HasUninstallCommand)
                {
                    builder.AppendLine($"- Uninstall command: {candidate.UninstallCommandLabel}");
                }

                if (candidate.HasUninstallTargetPath)
                {
                    builder.AppendLine($"- Uninstall target: {candidate.UninstallTargetLabel}");
                }

                if (candidate.HasResidueSummary)
                {
                    builder.AppendLine($"- Leftover footprint: {candidate.ResidueSummaryLabel}");
                }

                if (candidate.HasResidueFolderPath)
                {
                    builder.AppendLine($"- Primary leftover folder: {candidate.ResidueFolderPathLabel}");
                }

                if (candidate.CanExecuteInAppRepairPack)
                {
                    builder.AppendLine($"- Registry repair pack: {candidate.RepairActionLabelText}");
                    builder.AppendLine($"- Registry target: {candidate.RegistryPathLabel}");
                }

                if (candidate.HasOfficialResource)
                {
                    builder.AppendLine($"- Official repair link: {candidate.OfficialResourceUri}");
                }

                builder.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(advisory.ManualInput))
        {
            builder.AppendLine("## Manual input evidence");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(advisory.ManualInput.Trim());
            builder.AppendLine("```");
            builder.AppendLine();
        }

        if (advisory.OfficialResources.Count > 0)
        {
            builder.AppendLine("## Official repair links");
            builder.AppendLine();

            foreach (RepairResourceLink resource in advisory.OfficialResources)
            {
                builder.AppendLine($"- {resource.Title}: {resource.ResourceUri}");
            }
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}
