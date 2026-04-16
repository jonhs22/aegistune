namespace AegisTune.Core;

public sealed record DriverRemediationPlan(
    DriverRemediationSource RecommendedSource,
    DriverRebootGuidance RebootGuidance,
    string Summary,
    string SourceLabel,
    string SourceReason,
    string RollbackLabel,
    string RollbackDetail,
    IReadOnlyList<DriverVerificationStep> VerificationSteps)
{
    public string RebootGuidanceLabel => RebootGuidance switch
    {
        DriverRebootGuidance.NotExpected => "Reboot not expected",
        DriverRebootGuidance.MayBeRequired => "Reboot may be required",
        DriverRebootGuidance.LikelyRequired => "Reboot likely required",
        _ => "Reboot guidance unavailable"
    };

    public string RebootGuidanceDetail => RebootGuidance switch
    {
        DriverRebootGuidance.NotExpected => "A reboot is not usually required for this review path, but re-scan the device after any manual change.",
        DriverRebootGuidance.MayBeRequired => "Plan one re-scan immediately after the change and another after a reboot if Windows delays the device refresh.",
        DriverRebootGuidance.LikelyRequired => "Treat reboot verification as part of the remediation workflow. Re-scan after the package change and again after restart.",
        _ => "Keep reboot verification explicit after any driver change."
    };

    public string VerificationStatusLine => VerificationSteps.Count == 0
        ? "No verification steps are queued for this device."
        : $"{VerificationSteps.Count:N0} verification step(s) are queued for this remediation path.";
}
