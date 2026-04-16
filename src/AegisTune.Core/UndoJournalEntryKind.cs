namespace AegisTune.Core;

public enum UndoJournalEntryKind
{
    RestorePoint,
    RegistryRepair,
    RegistryRollback,
    DriverInstall,
    StartupDisable,
    StartupCleanup,
    StartupRestore,
    ApplicationUninstall,
    ApplicationResidueCleanup
}
