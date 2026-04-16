namespace AegisTune.Core;

public sealed record UndoJournalEntry(
    Guid Id,
    UndoJournalEntryKind Kind,
    string Title,
    DateTimeOffset OccurredAt,
    string StatusLine,
    string GuidanceLine,
    bool RestorePointCreated = false,
    bool RestorePointReused = false,
    string? RegistryBackupPath = null,
    string? RegistryTargetPath = null,
    string? ArtifactPath = null,
    string? TargetDetail = null,
    string? CommandLine = null)
{
    public string KindLabel => Kind switch
    {
        UndoJournalEntryKind.RestorePoint => "Restore point",
        UndoJournalEntryKind.RegistryRepair => "Registry repair",
        UndoJournalEntryKind.RegistryRollback => "Registry rollback",
        UndoJournalEntryKind.DriverInstall => "Driver install",
        UndoJournalEntryKind.StartupDisable => "Startup disable",
        UndoJournalEntryKind.StartupCleanup => "Startup cleanup",
        UndoJournalEntryKind.StartupRestore => "Startup restore",
        UndoJournalEntryKind.ApplicationUninstall => "App uninstall",
        UndoJournalEntryKind.ApplicationResidueCleanup => "App leftover cleanup",
        _ => "Undo item"
    };

    public string OccurredAtLabel => OccurredAt.ToLocalTime().ToString("g");

    public bool HasRegistryBackup => !string.IsNullOrWhiteSpace(RegistryBackupPath);

    public string? ArtifactPathToOpen => !string.IsNullOrWhiteSpace(RegistryBackupPath)
        ? RegistryBackupPath
        : ArtifactPath;

    public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactPathToOpen);

    public bool CanOpenArtifact =>
        !string.IsNullOrWhiteSpace(ArtifactPathToOpen)
        && (File.Exists(ArtifactPathToOpen) || Directory.Exists(ArtifactPathToOpen));

    public string RegistryBackupLabel => string.IsNullOrWhiteSpace(RegistryBackupPath)
        ? "No registry backup file is attached."
        : RegistryBackupPath!;

    public string RegistryTargetLabel => string.IsNullOrWhiteSpace(RegistryTargetPath)
        ? "No registry target recorded."
        : RegistryTargetPath!;

    public string ArtifactLabel => Kind switch
    {
        UndoJournalEntryKind.RegistryRepair or UndoJournalEntryKind.RegistryRollback =>
            string.IsNullOrWhiteSpace(RegistryBackupPath)
                ? string.Empty
                : $"Registry backup: {RegistryBackupPath}",
        UndoJournalEntryKind.DriverInstall =>
            string.IsNullOrWhiteSpace(ArtifactPath)
                ? string.Empty
                : $"INF path: {ArtifactPath}",
        UndoJournalEntryKind.StartupDisable =>
            string.IsNullOrWhiteSpace(ArtifactPath)
                ? string.Empty
                : $"Moved startup file: {ArtifactPath}",
        UndoJournalEntryKind.StartupCleanup =>
            string.IsNullOrWhiteSpace(ArtifactPath)
                ? string.Empty
                : $"Related startup artifact: {ArtifactPath}",
        UndoJournalEntryKind.StartupRestore =>
            string.IsNullOrWhiteSpace(ArtifactPath)
                ? string.Empty
                : $"Restored startup path: {ArtifactPath}",
        UndoJournalEntryKind.ApplicationUninstall =>
            string.IsNullOrWhiteSpace(ArtifactPath)
                ? string.Empty
                : $"Uninstall target: {ArtifactPath}",
        UndoJournalEntryKind.ApplicationResidueCleanup =>
            string.IsNullOrWhiteSpace(ArtifactPath)
                ? string.Empty
                : $"Residue quarantine: {ArtifactPath}",
        _ => string.Empty
    };

    public string TargetDetailLabel => Kind switch
    {
        UndoJournalEntryKind.RegistryRepair or UndoJournalEntryKind.RegistryRollback =>
            string.IsNullOrWhiteSpace(RegistryTargetPath)
                ? string.Empty
                : $"Registry target: {RegistryTargetPath}",
        UndoJournalEntryKind.DriverInstall =>
            string.IsNullOrWhiteSpace(TargetDetail)
                ? string.Empty
                : $"Target device: {TargetDetail}",
        UndoJournalEntryKind.StartupDisable or UndoJournalEntryKind.StartupCleanup =>
            string.IsNullOrWhiteSpace(TargetDetail)
                ? string.Empty
                : $"Startup source: {TargetDetail}",
        UndoJournalEntryKind.StartupRestore =>
            string.IsNullOrWhiteSpace(TargetDetail)
                ? string.Empty
                : $"Restored startup source: {TargetDetail}",
        UndoJournalEntryKind.ApplicationUninstall =>
            string.IsNullOrWhiteSpace(TargetDetail)
                ? string.Empty
                : $"App registration: {TargetDetail}",
        UndoJournalEntryKind.ApplicationResidueCleanup =>
            string.IsNullOrWhiteSpace(TargetDetail)
                ? string.Empty
                : $"Residue scope: {TargetDetail}",
        _ => string.IsNullOrWhiteSpace(TargetDetail)
            ? string.Empty
            : TargetDetail!
    };

    public string CommandLineSummary => string.IsNullOrWhiteSpace(CommandLine)
        ? string.Empty
        : Kind switch
        {
            UndoJournalEntryKind.StartupDisable or UndoJournalEntryKind.StartupCleanup => $"Launch command: {CommandLine}",
            UndoJournalEntryKind.StartupRestore => $"Restored launch command: {CommandLine}",
            UndoJournalEntryKind.ApplicationUninstall => $"Uninstall command: {CommandLine}",
            UndoJournalEntryKind.ApplicationResidueCleanup => $"Cleanup plan: {CommandLine}",
            _ => $"Command line: {CommandLine}"
        };

    public bool CanRunRegistryRollback =>
        Kind == UndoJournalEntryKind.RegistryRepair
        && HasRegistryBackup;

    public bool CanRunStartupRestore =>
        Kind == UndoJournalEntryKind.StartupDisable;

    public bool CanRunUndoAction =>
        Kind == UndoJournalEntryKind.RestorePoint
        || CanRunRegistryRollback
        || CanRunStartupRestore;

    public string SuggestedUndoActionLabel => Kind switch
    {
        UndoJournalEntryKind.RestorePoint => "Open System Restore",
        UndoJournalEntryKind.RegistryRepair => "Run registry rollback",
        UndoJournalEntryKind.RegistryRollback => "Rollback recorded",
        UndoJournalEntryKind.DriverInstall => "Install recorded",
        UndoJournalEntryKind.StartupDisable => "Restore startup entry",
        UndoJournalEntryKind.StartupCleanup => "Cleanup recorded",
        UndoJournalEntryKind.StartupRestore => "Restore recorded",
        UndoJournalEntryKind.ApplicationUninstall => "Uninstall recorded",
        UndoJournalEntryKind.ApplicationResidueCleanup => "Cleanup recorded",
        _ => "Review undo options"
    };

    public string OpenArtifactActionLabel => Kind switch
    {
        UndoJournalEntryKind.DriverInstall => "Open INF file",
        UndoJournalEntryKind.StartupDisable => "Open moved file",
        UndoJournalEntryKind.StartupCleanup => "Open related file",
        UndoJournalEntryKind.StartupRestore => "Open restored path",
        UndoJournalEntryKind.ApplicationUninstall => "Open uninstall target",
        UndoJournalEntryKind.ApplicationResidueCleanup => "Open quarantine folder",
        _ => "Open backup file"
    };
}
