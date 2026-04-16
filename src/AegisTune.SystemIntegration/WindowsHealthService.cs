using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

public sealed partial class WindowsHealthService : IWindowsHealthService
{
    private const int MaxCrashEvents = 8;
    private const int MaxWindowsUpdateEvents = 8;
    private const int MaxServiceCandidates = 10;
    private const int MaxScheduledTaskCandidates = 10;

    private static readonly string[] CrashProviders =
    [
        "Application Error",
        ".NET Runtime",
        "Windows Error Reporting"
    ];

    private readonly ISettingsStore _settingsStore;

    public WindowsHealthService(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<WindowsHealthSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);
        return await Task.Run(() => CollectSnapshot(settings, cancellationToken), cancellationToken);
    }

    private static WindowsHealthSnapshot CollectSnapshot(AppSettings settings, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        DateTimeOffset scannedAt = DateTimeOffset.Now;

        IReadOnlyList<WindowsHealthEventRecord> crashEvents = settings.IncludeCrashEvidenceInHealth
            ? ReadCrashEvents(settings.EffectiveHealthCrashLookbackDays, warnings, cancellationToken)
            : Array.Empty<WindowsHealthEventRecord>();
        IReadOnlyList<WindowsHealthEventRecord> windowsUpdateEvents = settings.IncludeWindowsUpdateIssuesInHealth
            ? ReadWindowsUpdateEvents(settings.EffectiveHealthWindowsUpdateLookbackDays, warnings, cancellationToken)
            : Array.Empty<WindowsHealthEventRecord>();
        IReadOnlyList<ServiceReviewRecord> serviceCandidates = settings.IncludeServiceReviewInHealth
            ? ReadServiceCandidates(warnings, cancellationToken)
            : Array.Empty<ServiceReviewRecord>();
        IReadOnlyList<ScheduledTaskReviewRecord> scheduledTaskCandidates = settings.IncludeScheduledTaskReviewInHealth
            ? ReadScheduledTaskCandidates(warnings, cancellationToken)
            : Array.Empty<ScheduledTaskReviewRecord>();

        string? warningMessage = warnings.Count == 0
            ? null
            : $"Health scan completed with {warnings.Count:N0} skipped source entr{(warnings.Count == 1 ? "y" : "ies")}.";

        if (!settings.IncludeCrashEvidenceInHealth
            && !settings.IncludeWindowsUpdateIssuesInHealth
            && !settings.IncludeServiceReviewInHealth
            && !settings.IncludeScheduledTaskReviewInHealth)
        {
            warningMessage = "Windows health review currently has no active evidence sources. Enable them in Settings.";
        }

        return new WindowsHealthSnapshot(
            crashEvents,
            windowsUpdateEvents,
            serviceCandidates,
            scheduledTaskCandidates,
            scannedAt,
            warningMessage);
    }

    private static IReadOnlyList<WindowsHealthEventRecord> ReadCrashEvents(
        int lookbackDays,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTime cutoff = DateTime.Now.AddDays(-lookbackDays);
            var events = new List<WindowsHealthEventRecord>();
            EventLogQuery query = new("Application", PathType.LogName)
            {
                ReverseDirection = true
            };

            using EventLogReader reader = new(query);

            for (EventRecord? record = reader.ReadEvent();
                 record is not null;
                 record = reader.ReadEvent())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (record.TimeCreated is not DateTime timeCreated)
                {
                    continue;
                }

                if (timeCreated < cutoff)
                {
                    break;
                }

                if (!CrashProviders.Contains(record.ProviderName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(record.LevelDisplayName, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string description;
                try
                {
                    description = record.FormatDescription() ?? string.Empty;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                string title = ExtractCrashTitle(description) ?? "Application crash signal";
                string detail = ExtractSummaryLine(description);

                events.Add(new WindowsHealthEventRecord(
                    title,
                    record.ProviderName ?? "Application",
                    record.Id,
                    record.LevelDisplayName ?? "Error",
                    detail,
                    new DateTimeOffset(timeCreated)));
            }

            return events
                .GroupBy(item => $"{item.Title}|{item.Source}|{item.EventId}|{item.ObservedAt:O}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(MaxCrashEvents)
                .ToArray();
        }
        catch (Exception ex)
        {
            warnings.Add($"Crash event log scan failed: {ex.Message}");
            return Array.Empty<WindowsHealthEventRecord>();
        }
    }

    private static IReadOnlyList<WindowsHealthEventRecord> ReadWindowsUpdateEvents(
        int lookbackDays,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTime cutoff = DateTime.Now.AddDays(-lookbackDays);
            var events = new List<WindowsHealthEventRecord>();
            EventLogQuery query = new("Microsoft-Windows-WindowsUpdateClient/Operational", PathType.LogName)
            {
                ReverseDirection = true
            };

            using EventLogReader reader = new(query);

            for (EventRecord? record = reader.ReadEvent();
                 record is not null;
                 record = reader.ReadEvent())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (record.TimeCreated is not DateTime timeCreated)
                {
                    continue;
                }

                if (timeCreated < cutoff)
                {
                    break;
                }

                if (!string.Equals(record.LevelDisplayName, "Warning", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(record.LevelDisplayName, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string description;
                try
                {
                    description = record.FormatDescription() ?? string.Empty;
                }
                catch
                {
                    continue;
                }

                events.Add(new WindowsHealthEventRecord(
                    $"Windows Update issue ({record.Id})",
                    record.ProviderName ?? "Windows Update",
                    record.Id,
                    record.LevelDisplayName ?? "Warning",
                    ExtractSummaryLine(description),
                    new DateTimeOffset(timeCreated)));
            }

            return events.Take(MaxWindowsUpdateEvents).ToArray();
        }
        catch (Exception ex)
        {
            warnings.Add($"Windows Update log scan failed: {ex.Message}");
            return Array.Empty<WindowsHealthEventRecord>();
        }
    }

    private static IReadOnlyList<ServiceReviewRecord> ReadServiceCandidates(
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var candidates = new List<ServiceReviewRecord>();

            using ManagementObjectSearcher searcher = new(
                "SELECT Name, DisplayName, PathName, State, StartMode FROM Win32_Service");
            using ManagementObjectCollection services = searcher.Get();

            foreach (ManagementObject service in services)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string name = service["Name"]?.ToString() ?? string.Empty;
                string displayName = service["DisplayName"]?.ToString() ?? name;
                string startMode = service["StartMode"]?.ToString() ?? string.Empty;
                string state = service["State"]?.ToString() ?? string.Empty;
                string rawPath = service["PathName"]?.ToString() ?? string.Empty;
                string? executablePath = CommandPathResolver.ResolveTargetPath(rawPath) ?? TryNormalizeRootedPath(rawPath);
                bool executablePathExists = !string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath);
                bool isWindowsService = !string.IsNullOrWhiteSpace(executablePath)
                    && executablePath.StartsWith(windowsPath, StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(executablePath) && !executablePathExists)
                {
                    candidates.Add(new ServiceReviewRecord(
                        name,
                        displayName,
                        startMode,
                        state,
                        executablePath,
                        executablePathExists,
                        "Service target file is missing."));
                    continue;
                }

                if (string.Equals(startMode, "Auto", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase)
                    && !isWindowsService)
                {
                    candidates.Add(new ServiceReviewRecord(
                        name,
                        displayName,
                        startMode,
                        state,
                        executablePath,
                        executablePathExists,
                        "Automatic third-party service is not running."));
                }
            }

            return candidates
                .OrderByDescending(candidate => !candidate.ExecutablePathExists)
                .ThenBy(candidate => candidate.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .Take(MaxServiceCandidates)
                .ToArray();
        }
        catch (Exception ex)
        {
            warnings.Add($"Service posture scan failed: {ex.Message}");
            return Array.Empty<ServiceReviewRecord>();
        }
    }

    private static IReadOnlyList<ScheduledTaskReviewRecord> ReadScheduledTaskCandidates(
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            string script =
                "$tasks = Get-ScheduledTask | Select-Object TaskName,TaskPath,State," +
                "@{Name='Execute';Expression={($_.Actions | Select-Object -First 1 -ExpandProperty Execute)}};" +
                "@($tasks) | ConvertTo-Json -Depth 4 -Compress";
            string stdout = RunPowerShellScript(script, cancellationToken);
            if (string.IsNullOrWhiteSpace(stdout))
            {
                return Array.Empty<ScheduledTaskReviewRecord>();
            }

            using JsonDocument document = JsonDocument.Parse(stdout);
            IEnumerable<JsonElement> taskElements = document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.EnumerateArray(),
                JsonValueKind.Object => [document.RootElement],
                _ => Array.Empty<JsonElement>()
            };

            var candidates = new List<ScheduledTaskReviewRecord>();

            foreach (JsonElement task in taskElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string taskName = GetJsonString(task, "TaskName");
                string taskPath = GetJsonString(task, "TaskPath");
                string state = GetJsonString(task, "State");
                string execute = GetJsonString(task, "Execute");

                if (string.IsNullOrWhiteSpace(taskName) || string.IsNullOrWhiteSpace(taskPath))
                {
                    continue;
                }

                if (taskPath.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? executePath = CommandPathResolver.ResolveTargetPath(execute) ?? TryNormalizeRootedPath(execute);
                bool executePathExists = !string.IsNullOrWhiteSpace(executePath) && File.Exists(executePath);

                if (!string.IsNullOrWhiteSpace(executePath) && !executePathExists)
                {
                    candidates.Add(new ScheduledTaskReviewRecord(
                        taskName,
                        taskPath,
                        state,
                        executePath,
                        executePathExists,
                        "Scheduled task action target is missing."));
                }
            }

            return candidates
                .OrderBy(candidate => candidate.DisplayPath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxScheduledTaskCandidates)
                .ToArray();
        }
        catch (Exception ex)
        {
            warnings.Add($"Scheduled task scan failed: {ex.Message}");
            return Array.Empty<ScheduledTaskReviewRecord>();
        }
    }

    private static string RunPowerShellScript(string script, CancellationToken cancellationToken)
    {
        string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                ? "PowerShell task scan returned a non-zero exit code."
                : stderr.Trim());
        }

        return stdout;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.ToString();
    }

    private static string ExtractSummaryLine(string description)
    {
        string[] lines = description
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
            ?? "No detail was available for this event.";
    }

    private static string? ExtractCrashTitle(string description)
    {
        Match faultingApplication = FaultingApplicationRegex().Match(description);
        if (faultingApplication.Success)
        {
            return Path.GetFileNameWithoutExtension(faultingApplication.Groups["exe"].Value);
        }

        Match genericApplication = GenericApplicationRegex().Match(description);
        return genericApplication.Success
            ? Path.GetFileNameWithoutExtension(genericApplication.Value)
            : null;
    }

    private static string? TryNormalizeRootedPath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(command.Trim().Trim('"'));
        return Path.IsPathRooted(expanded) ? expanded : null;
    }

    [GeneratedRegex(@"Faulting application name:\s*(?<exe>[^,\r\n]+?\.exe)", RegexOptions.IgnoreCase)]
    private static partial Regex FaultingApplicationRegex();

    [GeneratedRegex(@"[A-Za-z0-9._ -]+\.exe", RegexOptions.IgnoreCase)]
    private static partial Regex GenericApplicationRegex();
}
