using Microsoft.Win32;

namespace AegisTune.SystemIntegration;

internal static class RegistryPathUtility
{
    public static void ParseRegistryPath(string registryPath, out RegistryHive hive, out string subKeyPath)
    {
        const string hkcuPrefix = @"HKEY_CURRENT_USER\";
        const string hklmPrefix = @"HKEY_LOCAL_MACHINE\";
        const string hkcrPrefix = @"HKEY_CLASSES_ROOT\";

        if (registryPath.StartsWith(hkcuPrefix, StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            subKeyPath = registryPath[hkcuPrefix.Length..];
            return;
        }

        if (registryPath.StartsWith(hklmPrefix, StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            subKeyPath = registryPath[hklmPrefix.Length..];
            return;
        }

        if (registryPath.StartsWith(hkcrPrefix, StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.ClassesRoot;
            subKeyPath = registryPath[hkcrPrefix.Length..];
            return;
        }

        throw new InvalidOperationException($"Unsupported registry path: {registryPath}");
    }

    public static IEnumerable<RegistryView> GetViewsForHive(RegistryHive hive)
    {
        if (!Environment.Is64BitOperatingSystem || hive == RegistryHive.CurrentUser)
        {
            return [RegistryView.Default];
        }

        return [RegistryView.Registry64, RegistryView.Registry32];
    }
}
