using System.Text.RegularExpressions;

namespace AegisTune.Core;

public static partial class FirmwareVersionComparer
{
    public static bool AreEquivalent(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        string leftTrimmed = left.Trim();
        string rightTrimmed = right.Trim();
        if (string.Equals(leftTrimmed, rightTrimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedLeft = NormalizeVersionKey(leftTrimmed);
        string normalizedRight = NormalizeVersionKey(rightTrimmed);
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        HashSet<string> leftTokens = ExtractComparableTokens(leftTrimmed);
        HashSet<string> rightTokens = ExtractComparableTokens(rightTrimmed);
        return leftTokens.Overlaps(rightTokens);
    }

    private static HashSet<string> ExtractComparableTokens(string value)
    {
        HashSet<string> tokens = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ComparableTokenRegex().Matches(value))
        {
            string normalized = NormalizeVersionKey(match.Value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                tokens.Add(normalized);
            }
        }

        if (tokens.Count == 0)
        {
            string normalized = NormalizeVersionKey(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                tokens.Add(normalized);
            }
        }

        return tokens;
    }

    private static string NormalizeVersionKey(string value) =>
        new(
            value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

    [GeneratedRegex(@"[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)+|[A-Za-z]*\d+[A-Za-z0-9]*", RegexOptions.CultureInvariant)]
    private static partial Regex ComparableTokenRegex();
}
