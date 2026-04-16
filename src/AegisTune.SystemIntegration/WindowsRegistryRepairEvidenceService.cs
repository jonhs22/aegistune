using System.Runtime.Versioning;
using AegisTune.Core;
using Microsoft.Win32;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryRepairEvidenceService : IRegistryRepairEvidenceService
{
    private const int MaxAppPathCandidates = 12;
    private const int MaxShellCandidates = 12;

    private static readonly string[] ContextMenuHandlerRoots =
    [
        @"Software\Classes\*\shellex\ContextMenuHandlers",
        @"Software\Classes\Directory\shellex\ContextMenuHandlers",
        @"Software\Classes\Directory\Background\shellex\ContextMenuHandlers",
        @"Software\Classes\Drive\shellex\ContextMenuHandlers"
    ];

    private readonly IWindowsHealthService _windowsHealthService;

    public WindowsRegistryRepairEvidenceService(IWindowsHealthService windowsHealthService)
    {
        _windowsHealthService = windowsHealthService;
    }

    public async Task<IReadOnlyList<RepairCandidateRecord>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        WindowsHealthSnapshot healthSnapshot = await _windowsHealthService.GetSnapshotAsync(cancellationToken);

        List<RepairCandidateRecord> candidates = [];
        candidates.AddRange(BuildBrokenServiceCandidates(healthSnapshot.ServiceCandidates));
        candidates.AddRange(CollectStaleAppPathCandidates());
        candidates.AddRange(CollectBrokenContextMenuHandlerCandidates());

        return candidates
            .GroupBy(candidate => $"{candidate.Title}|{candidate.RegistryPathLabel}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<RepairCandidateRecord> BuildBrokenServiceCandidates(IReadOnlyList<ServiceReviewRecord> services)
    {
        foreach (ServiceReviewRecord service in services.Where(candidate => !candidate.ExecutablePathExists))
        {
            string registryPath = $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{service.Name}";
            yield return new RepairCandidateRecord(
                $"Disable broken service registration: {service.DisplayTitle}",
                "Services & registry",
                RiskLevel.Review,
                true,
                $"{service.DisplayTitle} points to a missing service target: {service.ExecutablePathLabel}",
                "Back up the service key and set Start=Disabled so Windows stops trying to launch a missing service target.",
                registryPath,
                RegistryRepairPackKind: RegistryRepairPackKind.SetDwordValue,
                RegistryPath: registryPath,
                RegistryValueName: "Start",
                RegistryDwordValue: 4,
                RepairActionLabel: "Back up + disable service");
        }
    }

    private static IEnumerable<RepairCandidateRecord> CollectStaleAppPathCandidates()
    {
        List<RepairCandidateRecord> candidates = [];

        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (RegistryView view in RegistryPathUtility.GetViewsForHive(hive))
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? appPathsKey = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths");
                if (appPathsKey is null)
                {
                    continue;
                }

                foreach (string subKeyName in appPathsKey.GetSubKeyNames())
                {
                    using RegistryKey? candidateKey = appPathsKey.OpenSubKey(subKeyName);
                    if (candidateKey is null)
                    {
                        continue;
                    }

                    string? defaultValue = candidateKey.GetValue(null)?.ToString();
                    string? executablePath = ResolvePath(defaultValue);
                    if (string.IsNullOrWhiteSpace(executablePath) || File.Exists(executablePath))
                    {
                        continue;
                    }

                    string registryPath = $"{GetHiveLabel(hive)}\\Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\{subKeyName}";
                    candidates.Add(new RepairCandidateRecord(
                        $"Remove stale App Paths entry: {subKeyName}",
                        "Registry & shell",
                        RiskLevel.Review,
                        hive == RegistryHive.LocalMachine,
                        $"App Paths still points to a missing executable: {executablePath}",
                        "Back up this App Paths key and remove the stale shell launch registration after one final review.",
                        registryPath,
                        ApplicationPath: executablePath,
                        ApplicationPathExists: false,
                        RegistryRepairPackKind: RegistryRepairPackKind.RemoveRegistryKey,
                        RegistryPath: registryPath,
                        RepairActionLabel: "Back up + remove App Paths entry"));

                    if (candidates.Count >= MaxAppPathCandidates)
                    {
                        return candidates;
                    }
                }
            }
        }

        return candidates;
    }

    private static IEnumerable<RepairCandidateRecord> CollectBrokenContextMenuHandlerCandidates()
    {
        List<RepairCandidateRecord> candidates = [];

        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (string handlerRoot in ContextMenuHandlerRoots)
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using RegistryKey? rootKey = baseKey.OpenSubKey(handlerRoot);
                if (rootKey is null)
                {
                    continue;
                }

                foreach (string subKeyName in rootKey.GetSubKeyNames())
                {
                    using RegistryKey? handlerKey = rootKey.OpenSubKey(subKeyName);
                    if (handlerKey is null)
                    {
                        continue;
                    }

                    string clsid = handlerKey.GetValue(null)?.ToString() ?? subKeyName;
                    if (string.IsNullOrWhiteSpace(clsid))
                    {
                        continue;
                    }

                    string? dllPath = ResolveShellExtensionPath(clsid);
                    if (string.IsNullOrWhiteSpace(dllPath) || File.Exists(dllPath))
                    {
                        continue;
                    }

                    string registryPath = $@"{GetHiveLabel(hive)}\{handlerRoot}\{subKeyName}";
                    candidates.Add(new RepairCandidateRecord(
                        $"Remove broken context-menu handler: {subKeyName}",
                        "Registry & shell",
                        RiskLevel.Review,
                        hive == RegistryHive.LocalMachine,
                        $"Explorer context-menu handler points to a missing shell extension DLL: {dllPath}",
                        "Back up the handler key and remove this stale Explorer shell registration so context menus stop loading a missing DLL.",
                        registryPath,
                        ApplicationPath: dllPath,
                        ApplicationPathExists: false,
                        RegistryRepairPackKind: RegistryRepairPackKind.RemoveRegistryKey,
                        RegistryPath: registryPath,
                        RepairActionLabel: "Back up + remove shell handler"));

                    if (candidates.Count >= MaxShellCandidates)
                    {
                        return candidates;
                    }
                }
            }
        }

        return candidates;
    }

    private static string? ResolveShellExtensionPath(string clsid)
    {
        string normalizedClsid = clsid.Trim().Trim('"');
        if (!normalizedClsid.StartsWith('{'))
        {
            normalizedClsid = $"{{{normalizedClsid.Trim('{', '}')}}}";
        }

        foreach (RegistryView view in RegistryPathUtility.GetViewsForHive(RegistryHive.ClassesRoot))
        {
            using RegistryKey classesRoot = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
            using RegistryKey? inProcKey = classesRoot.OpenSubKey($@"CLSID\{normalizedClsid}\InprocServer32");
            string? rawPath = inProcKey?.GetValue(null)?.ToString();
            string? resolved = ResolvePath(rawPath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolvePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(rawPath.Trim().Trim('"'));
        return Path.IsPathRooted(expanded)
            ? expanded
            : null;
    }

    private static string GetHiveLabel(RegistryHive hive) => hive switch
    {
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
        _ => hive.ToString()
    };
}
