namespace AegisTune.Core;

public sealed record FirmwareSafetyAssessment(
    string SupportIdentityLabel,
    string SystemDrive,
    string BitLockerStatusLine,
    string PowerStatusLine,
    DateTimeOffset CollectedAt,
    IReadOnlyList<FirmwareSafetyGate> Gates,
    string? WarningMessage = null,
    bool? IsBitLockerProtectionOn = null,
    bool? IsAcOnline = null,
    bool HasBattery = false,
    int? BatteryPercentage = null)
{
    public int BlockingGateCount => Gates.Count(gate => gate.Severity == FirmwareSafetyGateSeverity.Block);

    public int AttentionGateCount => Gates.Count(gate => gate.Severity == FirmwareSafetyGateSeverity.Attention);

    public int PassingGateCount => Gates.Count(gate => gate.Severity == FirmwareSafetyGateSeverity.Pass);

    public bool HasBlockingGate => BlockingGateCount > 0;

    public bool HasAttentionGate => AttentionGateCount > 0;

    public bool RequiresBitLockerSuspension => IsBitLockerProtectionOn == true;

    public string OverallPostureLabel => HasBlockingGate
        ? "Blocked until safety gates are cleared"
        : HasAttentionGate
            ? "Technician review required before flash"
            : "Ready for technician-controlled flash";

    public string SummaryLine => !string.IsNullOrWhiteSpace(WarningMessage)
        ? WarningMessage!
        : HasBlockingGate
            ? $"{BlockingGateCount:N0} blocking gate(s) and {AttentionGateCount:N0} review gate(s) were detected for {SupportIdentityLabel}."
            : HasAttentionGate
                ? $"{AttentionGateCount:N0} review gate(s) still need explicit confirmation before a BIOS flash for {SupportIdentityLabel}."
                : $"No blocking firmware safety gates are currently open for {SupportIdentityLabel}.";

    public string ChecklistPreview => Gates.Count == 0
        ? "No firmware safety checklist is available."
        : string.Join(
            Environment.NewLine,
            Gates.Select((gate, index) => $"{index + 1}. {gate.Title}: {gate.RecommendedAction}"));

    public string ClipboardText =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                $"Support identity: {SupportIdentityLabel}",
                $"Overall posture: {OverallPostureLabel}",
                $"Summary: {SummaryLine}",
                $"System drive: {SystemDrive}",
                $"BitLocker: {BitLockerStatusLine}",
                $"Power: {PowerStatusLine}",
                $"Collected at: {CollectedAt.ToLocalTime():g}",
                "Checklist:",
                ChecklistPreview
            });
}
