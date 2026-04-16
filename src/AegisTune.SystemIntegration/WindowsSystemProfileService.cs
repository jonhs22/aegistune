using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsSystemProfileService : ISystemProfileService
{
    public SystemProfile GetCurrentProfile()
    {
        Version version = Environment.OSVersion.Version;

        return new SystemProfile(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            $"Build {version.Build}",
            IsAdministrator());
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
