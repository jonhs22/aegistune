namespace AegisTune.Core;

public sealed record FirmwareSafetyGate(
    string Title,
    FirmwareSafetyGateSeverity Severity,
    string StatusLine,
    string Detail,
    string RecommendedAction)
{
    public string SeverityLabel => Severity switch
    {
        FirmwareSafetyGateSeverity.Pass => "Pass",
        FirmwareSafetyGateSeverity.Attention => "Needs attention",
        FirmwareSafetyGateSeverity.Block => "Blocked",
        FirmwareSafetyGateSeverity.Info => "Info",
        _ => "Unknown"
    };
}
