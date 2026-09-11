using System.Text.RegularExpressions;

namespace GiselX.Service;

public static partial class SelloutHeaderNormalizer
{
    internal const char SignatureSeparator = '\u001F';

    public static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(header.Trim(), " ").ToUpperInvariant();
    }

    public static string CreateSignature(IEnumerable<string?> headers)
    {
        var normalized = headers.Select(Normalize).ToList();
        while (normalized.Count > 0 && normalized[^1].Length == 0)
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return string.Join(SignatureSeparator, normalized);
    }

    public static IReadOnlyList<string> CreateDisplayHeaders(
        IEnumerable<string?> headers,
        IReadOnlySet<int>? allowedBlankColumns = null)
    {
        var result = new List<string>();
        var usedHeaders = new HashSet<string>(StringComparer.Ordinal);
        var firstDisplayNameByHeader = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in headers.Select((value, index) => new { value, index }))
        {
            var columnNumber = item.index + 1;
            if (string.IsNullOrWhiteSpace(item.value) &&
                (allowedBlankColumns?.Contains(columnNumber) ?? false))
            {
                result.Add(string.Empty);
                continue;
            }

            var originalHeader = string.IsNullOrWhiteSpace(item.value)
                ? $"Column {columnNumber}"
                : item.value!;
            var normalizedHeader = Normalize(originalHeader);
            var displayBase = WhitespaceRegex().Replace(originalHeader.Trim(), " ");

            if (usedHeaders.Add(normalizedHeader))
            {
                firstDisplayNameByHeader[normalizedHeader] = displayBase;
                result.Add(originalHeader);
                continue;
            }

            if (firstDisplayNameByHeader.TryGetValue(normalizedHeader, out var firstDisplayName))
            {
                displayBase = firstDisplayName;
            }
            var suffix = 2;
            string uniqueHeader;
            do
            {
                uniqueHeader = $"{displayBase} {suffix++}";
            } while (!usedHeaders.Add(Normalize(uniqueHeader)));

            firstDisplayNameByHeader.TryAdd(Normalize(uniqueHeader), uniqueHeader);
            result.Add(uniqueHeader);
        }

        return result;
    }

    public static IReadOnlyList<string> ValidateHeaders(
        IEnumerable<string?> headers,
        IReadOnlySet<int>? allowedBlankColumns = null)
    {
        var normalized = headers.Select(Normalize).ToList();
        while (normalized.Count > 0 && normalized[^1].Length == 0)
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        var errors = new List<string>();
        foreach (var item in normalized.Select((value, index) => new { value, index }))
        {
            if (item.value.Length == 0 && !(allowedBlankColumns?.Contains(item.index + 1) ?? false))
            {
                errors.Add($"Header column {item.index + 1} is blank.");
            }
        }

        var duplicates = normalized
            .Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        errors.AddRange(duplicates.Select(header => $"The normalized header '{header}' is duplicated."));
        return errors;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
