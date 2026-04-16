using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class DriverInstallVerificationService : IDriverInstallVerificationService
{
    public DriverInstallVerificationResult Verify(
        DriverDeviceRecord before,
        DriverDeviceRecord? after,
        DriverRepositoryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(candidate);

        if (after is null)
        {
            return new DriverInstallVerificationResult(
                candidate.InfPath,
                before.InstanceId,
                DriverInstallVerificationOutcome.VerificationInconclusive,
                before.ProviderLabel,
                "Device not found after install",
                before.VersionLabel,
                "Device not found after install",
                before.InfLabel,
                "Device not found after install",
                before.HealthLabel,
                "Device not found after install",
                before.ProblemCode,
                -1,
                ["Device missing after refresh"],
                "The device could not be found during the post-install re-audit.",
                "Re-scan the device tree and confirm whether the device instance ID changed, the hardware is disconnected, or the driver install requires a reboot.",
                DateTimeOffset.Now);
        }

        List<string> changedFields = [];

        AddChangedField(changedFields, "Provider", before.ProviderLabel, after.ProviderLabel);
        AddChangedField(changedFields, "Version", before.VersionLabel, after.VersionLabel);
        AddChangedField(changedFields, "INF", before.InfLabel, after.InfLabel);
        AddChangedField(changedFields, "Status", before.HealthLabel, after.HealthLabel);
        AddChangedField(changedFields, "Signing", before.SigningLabel, after.SigningLabel);
        AddChangedField(changedFields, "Signer", before.SignerLabel, after.SignerLabel);

        if (before.ProblemCode != after.ProblemCode)
        {
            changedFields.Add("Problem code");
        }

        bool driverChanged = !string.Equals(before.ProviderLabel, after.ProviderLabel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.VersionLabel, after.VersionLabel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.InfLabel, after.InfLabel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.SignerLabel, after.SignerLabel, StringComparison.OrdinalIgnoreCase);

        bool deviceImproved = (before.ProblemCode != 0 && after.ProblemCode == 0)
            || (!string.Equals(before.DeviceStatus, "OK", StringComparison.OrdinalIgnoreCase)
                && string.Equals(after.DeviceStatus, "OK", StringComparison.OrdinalIgnoreCase))
            || (before.RequiresPriorityReview && !after.RequiresPriorityReview)
            || (before.HasSigningConcern && !after.HasSigningConcern);

        DriverInstallVerificationOutcome outcome = deviceImproved
            ? DriverInstallVerificationOutcome.DeviceImproved
            : driverChanged
                ? DriverInstallVerificationOutcome.DriverChanged
                : changedFields.Count == 0
                    ? DriverInstallVerificationOutcome.NoChange
                    : DriverInstallVerificationOutcome.VerificationInconclusive;

        string summary = outcome switch
        {
            DriverInstallVerificationOutcome.DeviceImproved => "The device state improved after the install attempt.",
            DriverInstallVerificationOutcome.DriverChanged => "The effective driver fingerprint changed after the install attempt.",
            DriverInstallVerificationOutcome.NoChange => "The post-install re-audit did not detect any observable driver or device-state change.",
            _ => "The post-install re-audit found partial changes, but the result still needs technician review."
        };

        string notes = $"Before: {before.ProviderLabel} {before.VersionLabel} ({before.InfLabel}) with {before.HealthLabel}. "
            + $"After: {after.ProviderLabel} {after.VersionLabel} ({after.InfLabel}) with {after.HealthLabel}. "
            + $"Candidate: {Path.GetFileName(candidate.InfPath)}.";

        return new DriverInstallVerificationResult(
            candidate.InfPath,
            before.InstanceId,
            outcome,
            before.ProviderLabel,
            after.ProviderLabel,
            before.VersionLabel,
            after.VersionLabel,
            before.InfLabel,
            after.InfLabel,
            before.HealthLabel,
            after.HealthLabel,
            before.ProblemCode,
            after.ProblemCode,
            changedFields,
            summary,
            notes,
            DateTimeOffset.Now);
    }

    private static void AddChangedField(List<string> changedFields, string label, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add(label);
        }
    }
}
