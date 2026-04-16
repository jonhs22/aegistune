using System.Text.RegularExpressions;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

public sealed class LocalDriverDepotService : IDriverDepotService
{
    private static readonly Regex DeviceIdentifierRegex = new(
        @"(?<![A-Z0-9])([A-Z][A-Z0-9]{1,15}\\[A-Z0-9_&\.\-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public Task<DriverDepotScanResult> ScanAsync(
        IReadOnlyList<string> repositoryRoots,
        IReadOnlyList<DriverDeviceRecord> devices,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ScanInternal(repositoryRoots, devices, cancellationToken), cancellationToken);

    private static DriverDepotScanResult ScanInternal(
        IReadOnlyList<string> repositoryRoots,
        IReadOnlyList<DriverDeviceRecord> devices,
        CancellationToken cancellationToken)
    {
        DateTimeOffset scannedAt = DateTimeOffset.Now;
        string[] configuredRoots = repositoryRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] activeRoots = configuredRoots
            .Where(Directory.Exists)
            .ToArray();

        if (configuredRoots.Length == 0)
        {
            return new DriverDepotScanResult(
                Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                new Dictionary<string, IReadOnlyList<DriverRepositoryCandidate>>(StringComparer.OrdinalIgnoreCase),
                scannedAt);
        }

        if (activeRoots.Length == 0)
        {
            return new DriverDepotScanResult(
                configuredRoots,
                Array.Empty<string>(),
                0,
                new Dictionary<string, IReadOnlyList<DriverRepositoryCandidate>>(StringComparer.OrdinalIgnoreCase),
                scannedAt,
                "None of the configured driver repository roots are currently accessible.");
        }

        List<DriverDepotPackage> packages = [];
        foreach (string root in activeRoots)
        {
            foreach (string infPath in EnumerateInfFiles(root))
            {
                cancellationToken.ThrowIfCancellationRequested();

                DriverDepotPackage? package = TryParsePackage(infPath, root);
                if (package is not null)
                {
                    packages.Add(package);
                }
            }
        }

        Dictionary<string, List<DriverDepotPackage>> packageLookup = BuildPackageLookup(packages);
        Dictionary<string, IReadOnlyList<DriverRepositoryCandidate>> candidatesByInstanceId = new(StringComparer.OrdinalIgnoreCase);
        foreach (DriverDeviceRecord device in devices)
        {
            candidatesByInstanceId[device.InstanceId] = MatchDevice(device, packageLookup);
        }

        string? warningMessage = packages.Count switch
        {
            0 when activeRoots.Length < configuredRoots.Length => "No INF packages were found, and some configured repository roots are currently unavailable.",
            0 => "No INF packages were found across the configured repository roots.",
            _ when activeRoots.Length < configuredRoots.Length => "Some configured repository roots are currently unavailable, but the accessible roots were scanned successfully.",
            _ => null
        };

        return new DriverDepotScanResult(
            configuredRoots,
            activeRoots,
            packages.Count,
            candidatesByInstanceId,
            scannedAt,
            warningMessage);
    }

    private static IReadOnlyList<DriverRepositoryCandidate> MatchDevice(
        DriverDeviceRecord device,
        IReadOnlyDictionary<string, List<DriverDepotPackage>> packageLookup)
    {
        Dictionary<string, CandidateMatchBuilder> candidates = new(StringComparer.OrdinalIgnoreCase);

        AddMatches(device.HardwareIds, packageLookup, candidates, isHardwareEvidence: true);
        AddMatches(device.CompatibleIds, packageLookup, candidates, isHardwareEvidence: false);

        return candidates.Values
            .Select(builder => builder.Build())
            .OrderByDescending(candidate => candidate.MatchPriority)
            .ThenByDescending(candidate => candidate.MatchedIdentifiers.Count)
            .ThenBy(candidate => candidate.ProviderLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static void AddMatches(
        IReadOnlyList<string>? deviceIdentifiers,
        IReadOnlyDictionary<string, List<DriverDepotPackage>> packageLookup,
        IDictionary<string, CandidateMatchBuilder> candidates,
        bool isHardwareEvidence)
    {
        if (deviceIdentifiers is null || deviceIdentifiers.Count == 0)
        {
            return;
        }

        foreach (string deviceIdentifier in deviceIdentifiers.Where(identifier => !string.IsNullOrWhiteSpace(identifier)))
        {
            bool isExactMatch = true;
            foreach (string lookupKey in ExpandLookupKeys(deviceIdentifier))
            {
                if (!packageLookup.TryGetValue(lookupKey, out List<DriverDepotPackage>? packages))
                {
                    isExactMatch = false;
                    continue;
                }

                DriverRepositoryMatchKind matchKind = ResolveMatchKind(isHardwareEvidence, isExactMatch);
                foreach (DriverDepotPackage package in packages)
                {
                    if (!candidates.TryGetValue(package.InfPath, out CandidateMatchBuilder? builder))
                    {
                        builder = new CandidateMatchBuilder(package);
                        candidates[package.InfPath] = builder;
                    }

                    builder.AddMatch(matchKind, lookupKey);
                }

                isExactMatch = false;
            }
        }
    }

    private static DriverRepositoryMatchKind ResolveMatchKind(bool isHardwareEvidence, bool isExactMatch) =>
        (isHardwareEvidence, isExactMatch) switch
        {
            (true, true) => DriverRepositoryMatchKind.ExactHardwareId,
            (true, false) => DriverRepositoryMatchKind.GenericHardwareId,
            (false, true) => DriverRepositoryMatchKind.ExactCompatibleId,
            _ => DriverRepositoryMatchKind.GenericCompatibleId
        };

    private static IEnumerable<string> ExpandLookupKeys(string deviceIdentifier)
    {
        string normalized = NormalizeIdentifier(deviceIdentifier);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase)
        {
            normalized
        };

        yield return normalized;

        int slashIndex = normalized.IndexOf('\\');
        if (slashIndex < 0 || slashIndex == normalized.Length - 1)
        {
            yield break;
        }

        string prefix = normalized[..(slashIndex + 1)];
        string[] segments = normalized[(slashIndex + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int length = segments.Length - 1; length >= 2; length--)
        {
            string candidate = prefix + string.Join('&', segments.Take(length));
            if (yielded.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static Dictionary<string, List<DriverDepotPackage>> BuildPackageLookup(IEnumerable<DriverDepotPackage> packages)
    {
        Dictionary<string, List<DriverDepotPackage>> lookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (DriverDepotPackage package in packages)
        {
            foreach (string identifier in package.DeviceIdentifiers)
            {
                if (!lookup.TryGetValue(identifier, out List<DriverDepotPackage>? packageList))
                {
                    packageList = [];
                    lookup[identifier] = packageList;
                }

                packageList.Add(package);
            }
        }

        return lookup;
    }

    private static IEnumerable<string> EnumerateInfFiles(string root)
    {
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            string current = pendingDirectories.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current, "*.inf");
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string directory in directories)
            {
                pendingDirectories.Push(directory);
            }
        }
    }

    private static DriverDepotPackage? TryParsePackage(string infPath, string repositoryRoot)
    {
        try
        {
            Dictionary<string, string> strings = new(StringComparer.OrdinalIgnoreCase);
            List<InfLine> lines = [];
            string currentSection = string.Empty;

            foreach (string rawLine in File.ReadLines(infPath))
            {
                string line = StripComments(rawLine).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    currentSection = line[1..^1].Trim();
                    continue;
                }

                lines.Add(new InfLine(currentSection, line));

                if (currentSection.StartsWith("Strings", StringComparison.OrdinalIgnoreCase)
                    && TrySplitKeyValue(line, out string key, out string value))
                {
                    strings[key] = TrimInfValue(value);
                }
            }

            string provider = string.Empty;
            string driverClass = string.Empty;
            string driverVersion = string.Empty;
            string? catalogFile = null;
            HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);

            foreach (InfLine infLine in lines)
            {
                if (infLine.Section.Equals("Version", StringComparison.OrdinalIgnoreCase)
                    && TrySplitKeyValue(infLine.Value, out string key, out string value))
                {
                    string resolvedValue = ResolveInfValue(value, strings);
                    switch (key)
                    {
                        case "Provider":
                            provider = resolvedValue;
                            break;
                        case "Class":
                            driverClass = resolvedValue;
                            break;
                        case "DriverVer":
                            driverVersion = ExtractDriverVersion(resolvedValue);
                            break;
                        case "CatalogFile":
                        case "CatalogFile.NT":
                        case "CatalogFile.NTx86":
                        case "CatalogFile.NTamd64":
                        case "CatalogFile.NTarm64":
                            catalogFile ??= resolvedValue;
                            break;
                    }
                }

                foreach (Match match in DeviceIdentifierRegex.Matches(infLine.Value))
                {
                    string normalizedIdentifier = NormalizeIdentifier(match.Groups[1].Value);
                    if (LooksLikeDeviceIdentifier(normalizedIdentifier))
                    {
                        identifiers.Add(normalizedIdentifier);
                    }
                }
            }

            if (identifiers.Count == 0)
            {
                return null;
            }

            return new DriverDepotPackage(
                infPath,
                repositoryRoot,
                provider,
                driverClass,
                driverVersion,
                catalogFile,
                identifiers.ToArray());
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TrySplitKeyValue(string line, out string key, out string value)
    {
        int separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = line[..separatorIndex].Trim();
        value = line[(separatorIndex + 1)..].Trim();
        return true;
    }

    private static string ResolveInfValue(string value, IReadOnlyDictionary<string, string> strings)
    {
        string trimmedValue = TrimInfValue(value);
        if (trimmedValue.Length > 2
            && trimmedValue.StartsWith('%')
            && trimmedValue.EndsWith('%'))
        {
            string token = trimmedValue[1..^1];
            if (strings.TryGetValue(token, out string? resolvedValue))
            {
                return resolvedValue;
            }
        }

        return trimmedValue;
    }

    private static string TrimInfValue(string value) =>
        value.Trim().Trim('"');

    private static string ExtractDriverVersion(string value)
    {
        string[] segments = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0
            ? TrimInfValue(value)
            : segments[^1];
    }

    private static string StripComments(string line)
    {
        int commentIndex = line.IndexOf(';');
        return commentIndex < 0 ? line : line[..commentIndex];
    }

    private static string NormalizeIdentifier(string value) =>
        value.Trim().Trim('"');

    private static bool LooksLikeDeviceIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('\\'))
        {
            return false;
        }

        int slashIndex = value.IndexOf('\\');
        if (slashIndex < 1 || slashIndex == value.Length - 1)
        {
            return false;
        }

        string enumerator = value[..slashIndex];
        return enumerator.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private sealed record DriverDepotPackage(
        string InfPath,
        string RepositoryRoot,
        string Provider,
        string DriverClass,
        string DriverVersion,
        string? CatalogFile,
        IReadOnlyList<string> DeviceIdentifiers);

    private sealed class CandidateMatchBuilder
    {
        private readonly HashSet<string> _matchedIdentifiers = new(StringComparer.OrdinalIgnoreCase);
        private readonly DriverDepotPackage _package;
        private DriverRepositoryMatchKind _bestMatchKind = DriverRepositoryMatchKind.GenericCompatibleId;

        public CandidateMatchBuilder(DriverDepotPackage package)
        {
            _package = package;
        }

        public void AddMatch(DriverRepositoryMatchKind matchKind, string matchedIdentifier)
        {
            if ((int)matchKind > (int)_bestMatchKind)
            {
                _bestMatchKind = matchKind;
            }

            if (!string.IsNullOrWhiteSpace(matchedIdentifier))
            {
                _matchedIdentifiers.Add(matchedIdentifier);
            }
        }

        public DriverRepositoryCandidate Build() =>
            new(
                _package.InfPath,
                _package.RepositoryRoot,
                _package.Provider,
                _package.DriverClass,
                _package.DriverVersion,
                _package.CatalogFile,
                _bestMatchKind,
                _matchedIdentifiers.ToArray());
    }

    private sealed record InfLine(string Section, string Value);
}
