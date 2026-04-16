namespace AegisTune.SystemIntegration;

internal static class CommandPathResolver
{
    private static readonly string[] ExecutableExtensions = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".lnk", ".url", ".msi"];

    public static string? ResolveTargetPath(string? command)
    {
        string expanded = Environment.ExpandEnvironmentVariables(command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expanded))
        {
            return null;
        }

        if (expanded.StartsWith('"'))
        {
            int closingQuote = expanded.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return NormalizePath(expanded[1..closingQuote]);
            }
        }

        foreach (string extension in ExecutableExtensions)
        {
            int extensionIndex = expanded.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (extensionIndex < 0)
            {
                continue;
            }

            string candidatePath = expanded[..(extensionIndex + extension.Length)].Trim().Trim('"');
            string? normalized = NormalizePath(candidatePath);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    public static bool TrySplitCommand(string? command, out string fileName, out string arguments)
    {
        string expanded = Environment.ExpandEnvironmentVariables(command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expanded))
        {
            fileName = string.Empty;
            arguments = string.Empty;
            return false;
        }

        if (expanded.StartsWith('"'))
        {
            int closingQuote = expanded.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                fileName = string.Empty;
                arguments = string.Empty;
                return false;
            }

            fileName = expanded[1..closingQuote];
            arguments = expanded[(closingQuote + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(fileName);
        }

        int firstWhitespace = expanded.IndexOfAny([' ', '\t']);
        if (firstWhitespace < 0)
        {
            fileName = expanded;
            arguments = string.Empty;
            return true;
        }

        fileName = expanded[..firstWhitespace].Trim();
        arguments = expanded[(firstWhitespace + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(fileName);
    }

    private static string? NormalizePath(string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return null;
        }

        string normalized = Environment.ExpandEnvironmentVariables(candidatePath.Trim().Trim('"'));
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        foreach (string pathSegment in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string combined = Path.Combine(pathSegment, normalized);
            if (File.Exists(combined))
            {
                return combined;
            }
        }

        return null;
    }
}
