using AegisTune.Core;

namespace AegisTune.RepairEngine;

public sealed class EvidenceBasedRepairScanner : IRepairScanner
{
    private readonly IInstalledApplicationInventoryService _installedApplicationInventoryService;
    private readonly IRegistryRepairEvidenceService _registryRepairEvidenceService;
    private readonly IRepairEvidenceService _repairEvidenceService;
    private readonly ISettingsStore _settingsStore;
    private readonly IStartupInventoryService _startupInventoryService;

    public EvidenceBasedRepairScanner(
        IInstalledApplicationInventoryService installedApplicationInventoryService,
        IRegistryRepairEvidenceService registryRepairEvidenceService,
        IRepairEvidenceService repairEvidenceService,
        ISettingsStore settingsStore,
        IStartupInventoryService startupInventoryService)
    {
        _installedApplicationInventoryService = installedApplicationInventoryService;
        _registryRepairEvidenceService = registryRepairEvidenceService;
        _repairEvidenceService = repairEvidenceService;
        _settingsStore = settingsStore;
        _startupInventoryService = startupInventoryService;
    }

    public async Task<RepairScanResult> ScanAsync(
        AppInventorySnapshot? appInventory = null,
        StartupInventorySnapshot? startupInventory = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset scannedAt = DateTimeOffset.Now;

        try
        {
            appInventory ??= await _installedApplicationInventoryService.GetSnapshotAsync(cancellationToken);
            startupInventory ??= await _startupInventoryService.GetSnapshotAsync(cancellationToken);
            AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);

            List<RepairCandidateRecord> candidates = BuildCandidates(appInventory, startupInventory, settings);
            if (settings.IncludeRegistryResidueReview)
            {
                candidates.AddRange(await _registryRepairEvidenceService.GetCandidatesAsync(cancellationToken));
            }

            IReadOnlyList<DependencyRepairSignal> dependencySignals =
                await _repairEvidenceService.GetDependencySignalsAsync(cancellationToken);
            candidates.AddRange(DependencyRepairAdvisor.BuildCandidates(appInventory, dependencySignals));
            return new RepairScanResult(candidates, scannedAt);
        }
        catch (Exception ex)
        {
            return new RepairScanResult(Array.Empty<RepairCandidateRecord>(), scannedAt, $"Repair scan failed: {ex.Message}");
        }
    }

    private static List<RepairCandidateRecord> BuildCandidates(
        AppInventorySnapshot appInventory,
        StartupInventorySnapshot startupInventory,
        AppSettings settings)
    {
        var candidates = new List<RepairCandidateRecord>();

        candidates.AddRange(startupInventory.Entries
            .Where(entry => entry.IsOrphaned)
            .Select(entry => new RepairCandidateRecord(
                $"Remove orphaned startup entry: {entry.Name}",
                "Startup",
                RiskLevel.Safe,
                RequiresAdministrator: entry.ScopeLabel.Contains("All users", StringComparison.OrdinalIgnoreCase),
                $"{entry.Source} still points to a missing target: {entry.ResolvedTargetLabel}",
                "Remove the stale startup entry after one final review.",
                entry.SourceLocationLabel)));

        if (settings.IncludeRegistryResidueReview)
        {
            candidates.AddRange(appInventory.Applications
                .Where(app => app.NeedsLeftoverReview)
                .Select(app => new RepairCandidateRecord(
                    $"Registry and leftover review: {app.DisplayName}",
                    "Registry & leftovers",
                    RiskLevel.Review,
                    RequiresAdministrator: app.ScopeLabel.Contains("All users", StringComparison.OrdinalIgnoreCase),
                    app.HasFilesystemResidue
                        ? $"The uninstall registration for {app.DisplayName} is stale or incomplete, and leftover folders are still present. {app.FilesystemResidueSummaryLabel}"
                        : $"The uninstall registry entry for {app.DisplayName} is still present, but both the install folder and uninstall target are missing.",
                    app.HasFilesystemResidue
                        ? "Inspect the leftover folders first, then remove stale uninstall registrations or residual files only after one final review."
                        : "Inspect the stale uninstall registration before removing leftovers from the registry or filesystem.",
                    app.RegistryKeyPath,
                    app.DisplayName,
                    null,
                    false,
                    app.InstallLocation,
                    app.InstallLocationExists,
                    app.UninstallCommand,
                    app.ResolvedUninstallTargetPath,
                    app.UninstallTargetExists,
                    app.PrimaryResidue?.Path,
                    app.HasPrimaryResiduePath,
                    app.FilesystemResidueSummaryLabel)));
        }

        return candidates
            .OrderBy(candidate => candidate.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
