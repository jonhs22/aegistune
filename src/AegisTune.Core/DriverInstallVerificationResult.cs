namespace AegisTune.Core;

public sealed record DriverInstallVerificationResult(
    string CandidateInfPath,
    string DeviceInstanceId,
    DriverInstallVerificationOutcome Outcome,
    string BeforeProvider,
    string AfterProvider,
    string BeforeVersion,
    string AfterVersion,
    string BeforeInf,
    string AfterInf,
    string BeforeStatus,
    string AfterStatus,
    int BeforeProblemCode,
    int AfterProblemCode,
    IReadOnlyList<string> ChangedFields,
    string Summary,
    string Notes,
    DateTimeOffset VerifiedAt)
{
    public string OutcomeLabel => Outcome switch
    {
        DriverInstallVerificationOutcome.DeviceImproved => "Device improved",
        DriverInstallVerificationOutcome.DriverChanged => "Driver changed",
        DriverInstallVerificationOutcome.VerificationInconclusive => "Verification inconclusive",
        _ => "No observable change"
    };

    public string ChangedFieldsLabel => ChangedFields.Count == 0
        ? "No changed fields detected"
        : string.Join(", ", ChangedFields);

    public string VerifiedAtLabel => VerifiedAt.ToLocalTime().ToString("g");
}
