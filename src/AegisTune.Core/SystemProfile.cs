namespace AegisTune.Core;

public sealed record SystemProfile(
    string DeviceName,
    string OperatingSystem,
    string BuildLabel,
    bool IsAdministrator)
{
    public string AdministratorLabel => IsAdministrator ? "Elevated session" : "Standard session";
}
