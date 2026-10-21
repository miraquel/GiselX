using System.Text.RegularExpressions;
using GiselX.Domain;

namespace GiselX.Service;

public static partial class StockTextNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Trims and collapses internal whitespace to one space.</summary>
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Whitespace().Replace(value.Trim(), " ");

    /// <summary>
    /// Splits a ';'-delimited list, normalising each entry, dropping blanks and
    /// de-duplicating case-insensitively while keeping the first spelling.
    /// </summary>
    public static IReadOnlyList<string> SplitList(string? delimited)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(delimited))
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in delimited.Split(';'))
        {
            var normalized = Normalize(part);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}

public sealed record StockAreaResolution(int? AreaId, string? Error);

public sealed class StockAreaMatcher
{
    private readonly Dictionary<string, List<StockArea>> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly string companyName;

    public StockAreaMatcher(IEnumerable<StockArea> activeAreas, string companyName)
    {
        this.companyName = companyName;
        foreach (var area in activeAreas.Where(a => a.IsActive))
        {
            var keys = new[] { StockTextNormalizer.Normalize(area.Name) }
                .Concat(StockTextNormalizer.SplitList(area.Aliases))
                .Where(k => k.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                if (!index.TryGetValue(key, out var list))
                {
                    index[key] = list = [];
                }

                list.Add(area);
            }
        }
    }

    public StockAreaResolution Resolve(string? cellText)
    {
        var text = StockTextNormalizer.Normalize(cellText);
        if (text.Length == 0)
        {
            return new StockAreaResolution(null, "Area is empty.");
        }

        if (!index.TryGetValue(text, out var matches))
        {
            return new StockAreaResolution(null, $"Area '{text}' is not registered for {companyName}.");
        }

        if (matches.Count > 1)
        {
            var names = string.Join(", ", matches.Select(a => a.Name));
            return new StockAreaResolution(null,
                $"Area '{text}' matches more than one area of {companyName}: {names}. Make the area names and aliases unique.");
        }

        return new StockAreaResolution(matches[0].Id, null);
    }
}
