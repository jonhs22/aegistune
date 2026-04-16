namespace AegisTune.Core;

public sealed record RiskyChangePreflightRequest(
    RiskyChangeType ChangeType,
    string Title,
    SystemRestoreIntent RestoreIntent);
