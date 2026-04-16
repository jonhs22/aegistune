using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

public sealed partial class WindowsRepairEvidenceService : IRepairEvidenceService
{
    private static readonly string[] ProviderNames =
    [
        "Application Error",
        "Application Popup",
        "SideBySide",
        "Windows Error Reporting",
        ".NET Runtime"
    ];

    public Task<IReadOnlyList<DependencyRepairSignal>> GetDependencySignalsAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadSignals(cancellationToken), cancellationToken);

    private static IReadOnlyList<DependencyRepairSignal> ReadSignals(CancellationToken cancellationToken)
    {
        DateTime cutoff = DateTime.Now.AddDays(-14);
        var signals = new List<DependencyRepairSignal>();

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

            if (!ProviderNames.Contains(record.ProviderName, StringComparer.OrdinalIgnoreCase))
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

            signals.AddRange(ExtractSignals(
                record.ProviderName ?? "Application",
                description,
                timeCreated));
        }

        return signals
            .GroupBy(
                signal => $"{signal.ApplicationPath}|{signal.ApplicationName}|{signal.DependencyName}|{signal.EvidenceSource}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ObservedAt).First())
            .ToArray();
    }

    private static IReadOnlyList<DependencyRepairSignal> ExtractSignals(
        string providerName,
        string description,
        DateTime timeCreated)
    {
        bool missingDependencyKeywords = LooksLikeMissingDependency(description);
        string? applicationPath = ExtractApplicationPath(description);
        string? applicationName = ExtractApplicationName(description, applicationPath);
        DateTimeOffset observedAt = new(timeCreated);

        if (description.Contains("side-by-side configuration is incorrect", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new DependencyRepairSignal(
                    "Microsoft Visual C++ runtime",
                    providerName,
                    description,
                    observedAt,
                    applicationName,
                    applicationPath)
            ];
        }

        List<string> dependencyNames = DllRegex()
            .Matches(description)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dependencyNames.Count == 0)
        {
            return Array.Empty<DependencyRepairSignal>();
        }

        return dependencyNames
            .Where(dependencyName => missingDependencyKeywords || LooksLikeKnownDependency(dependencyName))
            .Select(dependencyName => new DependencyRepairSignal(
                dependencyName,
                providerName,
                description,
                observedAt,
                applicationName,
                applicationPath))
            .ToArray();
    }

    private static bool LooksLikeKnownDependency(string dependencyName)
    {
        string fileName = dependencyName.ToLowerInvariant();
        return fileName.StartsWith("msvcp", StringComparison.Ordinal)
            || fileName.StartsWith("msvcr", StringComparison.Ordinal)
            || fileName.StartsWith("vcruntime", StringComparison.Ordinal)
            || fileName.StartsWith("concrt", StringComparison.Ordinal)
            || fileName.StartsWith("api-ms-win-crt", StringComparison.Ordinal)
            || fileName.Equals("ucrtbase.dll", StringComparison.Ordinal)
            || fileName.Equals("webview2loader.dll", StringComparison.Ordinal)
            || fileName.Equals("d3dx9_43.dll", StringComparison.Ordinal)
            || fileName.Equals("d3dcompiler_43.dll", StringComparison.Ordinal)
            || fileName.Equals("xinput1_3.dll", StringComparison.Ordinal)
            || fileName.Equals("xaudio2_7.dll", StringComparison.Ordinal);
    }

    private static bool LooksLikeMissingDependency(string description)
    {
        string message = description.ToLowerInvariant();
        return message.Contains("missing", StringComparison.Ordinal)
            || message.Contains("not found", StringComparison.Ordinal)
            || message.Contains("could not be found", StringComparison.Ordinal)
            || message.Contains("module was not found", StringComparison.Ordinal)
            || message.Contains("failed to load", StringComparison.Ordinal)
            || message.Contains("unable to start", StringComparison.Ordinal)
            || message.Contains("side-by-side configuration is incorrect", StringComparison.Ordinal)
            || message.Contains("dependent assembly", StringComparison.Ordinal);
    }

    private static string? ExtractApplicationPath(string description)
    {
        Match match = ApplicationPathRegex().Match(description);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractApplicationName(string description, string? applicationPath)
    {
        if (!string.IsNullOrWhiteSpace(applicationPath))
        {
            return Path.GetFileNameWithoutExtension(applicationPath);
        }

        Match faultingNameMatch = FaultingApplicationNameRegex().Match(description);
        if (faultingNameMatch.Success)
        {
            return Path.GetFileNameWithoutExtension(faultingNameMatch.Groups["exe"].Value);
        }

        Match genericExeMatch = ExeRegex().Match(description);
        return genericExeMatch.Success
            ? Path.GetFileNameWithoutExtension(genericExeMatch.Value)
            : null;
    }

    [GeneratedRegex(@"[A-Za-z0-9._-]+\.dll", RegexOptions.IgnoreCase)]
    private static partial Regex DllRegex();

    [GeneratedRegex(@"[A-Z]:\\[^""\r\n]+?\.exe", RegexOptions.IgnoreCase)]
    private static partial Regex ApplicationPathRegex();

    [GeneratedRegex(@"Faulting application name:\s*(?<exe>[^,\r\n]+?\.exe)", RegexOptions.IgnoreCase)]
    private static partial Regex FaultingApplicationNameRegex();

    [GeneratedRegex(@"[A-Za-z0-9._ -]+\.exe", RegexOptions.IgnoreCase)]
    private static partial Regex ExeRegex();
}
