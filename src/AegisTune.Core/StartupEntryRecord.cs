namespace AegisTune.Core;

public sealed record StartupEntryRecord(
    string Name,
    string LaunchCommand,
    string Source,
    string ScopeLabel,
    string? ResolvedTargetPath,
    bool TargetExists,
    bool IsOrphaned,
    StartupImpactLevel ImpactLevel,
    StartupEntryOrigin Origin = StartupEntryOrigin.Unknown,
    string? RegistryLocation = null,
    string? RegistryValueName = null,
    string? RegistryViewName = null,
    string? StartupFilePath = null,
    string? Notes = null)
{
    public string ImpactLabel => ImpactLevel switch
    {
        StartupImpactLevel.Low => "Low impact",
        StartupImpactLevel.Medium => "Medium impact",
        StartupImpactLevel.High => "High impact",
        StartupImpactLevel.Review => "Review target",
        _ => "Unknown"
    };

    public string StatusLabel => IsOrphaned
        ? "Orphaned target"
        : TargetExists
            ? "Resolved target"
            : ResolvedTargetPath is null
                ? "Manual review"
                : "Target missing";

    public string ResolvedTargetLabel => string.IsNullOrWhiteSpace(ResolvedTargetPath)
        ? "No direct target path could be resolved."
        : ResolvedTargetPath;

    public bool HasResolvedTargetPath => !string.IsNullOrWhiteSpace(ResolvedTargetPath);

    public bool CanOpenResolvedTarget => TargetExists && HasResolvedTargetPath;

    public string? StartupFolderPath => string.IsNullOrWhiteSpace(StartupFilePath)
        ? null
        : Path.GetDirectoryName(StartupFilePath);

    public bool HasStartupFolderPath => !string.IsNullOrWhiteSpace(StartupFolderPath);

    public bool CanDisableFromStartup =>
        !IsOrphaned
        && (Origin switch
        {
            StartupEntryOrigin.RegistryValue =>
                !string.IsNullOrWhiteSpace(RegistryLocation)
                && RegistryValueName is not null
                && !string.IsNullOrWhiteSpace(RegistryViewName),
            StartupEntryOrigin.StartupFolderFile =>
                !string.IsNullOrWhiteSpace(StartupFilePath),
            _ => false
        });

    public bool CanRemoveSafely =>
        IsOrphaned
        && (Origin switch
        {
            StartupEntryOrigin.RegistryValue =>
                !string.IsNullOrWhiteSpace(RegistryLocation)
                && RegistryValueName is not null
                && !string.IsNullOrWhiteSpace(RegistryViewName),
            StartupEntryOrigin.StartupFolderFile =>
                !string.IsNullOrWhiteSpace(StartupFilePath),
            _ => false
        });

    public string SourceLocationLabel => Origin switch
    {
        StartupEntryOrigin.RegistryValue when !string.IsNullOrWhiteSpace(RegistryLocation) && RegistryValueName is not null =>
            $"{RegistryLocation} [{(string.IsNullOrEmpty(RegistryValueName) ? "(Default)" : RegistryValueName)}]",
        StartupEntryOrigin.StartupFolderFile when !string.IsNullOrWhiteSpace(StartupFilePath) => StartupFilePath,
        _ => Source
    };

    public string RemovalActionLabel => CanRemoveSafely
        ? "Remove stale entry"
        : "Review only";

    public string DisableActionLabel => CanDisableFromStartup
        ? "Disable from startup"
        : "Review only";

    public string EntryBrief =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"Startup entry: {Name}",
                $"Impact: {ImpactLabel}",
                $"Status: {StatusLabel}",
                $"Scope: {ScopeLabel}",
                $"Source: {Source}",
                $"Source location: {SourceLocationLabel}",
                $"Resolved target: {ResolvedTargetLabel}",
                $"Launch command: {LaunchCommand}",
                $"Notes: {Notes ?? "No additional startup note."}"
            });

    public string SelectionKey => $"{Name}|{SourceLocationLabel}|{ResolvedTargetPath}";
}
