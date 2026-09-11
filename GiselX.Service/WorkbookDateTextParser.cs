using System.Globalization;

namespace GiselX.Service;

/// <summary>
/// Reads the date shapes distributor workbooks actually contain, which is wider than any single culture
/// covers. Excel date cells arrive as OLE Automation serials; text cells arrive as Indonesian or English
/// month names, in local abbreviations CLDR does not know ("Agt", "Nop", "Peb"), with two-digit years,
/// and — the failure that motivated this parser — separated by non-breaking spaces that report exporters
/// emit and that <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/>
/// refuses. Month names are matched from an explicit table rather than from culture data, so the result
/// does not shift when the host's ICU version changes.
/// </summary>
public static class WorkbookDateTextParser
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    /// <summary>Years given with two digits: 00-49 are 2000s, 50-99 are 1900s.</summary>
    private const int TwoDigitYearPivot = 49;

    private static readonly IReadOnlyDictionary<string, int> MonthNames =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["jan"] = 1, ["januari"] = 1, ["january"] = 1,
            ["feb"] = 2, ["februari"] = 2, ["february"] = 2, ["peb"] = 2, ["pebruari"] = 2,
            ["mar"] = 3, ["mrt"] = 3, ["maret"] = 3, ["march"] = 3,
            ["apr"] = 4, ["april"] = 4,
            ["mei"] = 5, ["may"] = 5,
            ["jun"] = 6, ["juni"] = 6, ["june"] = 6,
            ["jul"] = 7, ["juli"] = 7, ["july"] = 7,
            ["agu"] = 8, ["agt"] = 8, ["agus"] = 8, ["agust"] = 8, ["agustus"] = 8,
            ["aug"] = 8, ["august"] = 8,
            ["sep"] = 9, ["sept"] = 9, ["september"] = 9,
            ["okt"] = 10, ["oct"] = 10, ["oktober"] = 10, ["october"] = 10,
            ["nov"] = 11, ["nop"] = 11, ["november"] = 11, ["nopember"] = 11,
            ["des"] = 12, ["dec"] = 12, ["desember"] = 12, ["december"] = 12
        };

    /// <summary>
    /// Parentheses are separators rather than content: a date never contains one, but the note a stock
    /// form writes beside an expiry date often does ("31/12/2026 (7 ctn 7 pcs)").
    /// </summary>
    private static readonly char[] Separators = [' ', '-', '/', '.', ',', '(', ')'];

    public static bool TryParse(string? source, out DateTime value)
    {
        value = default;
        var text = Normalize(source);
        if (text.Length == 0) return false;

        // A date cell in an .xlsx holds a serial, not text, so this is the common path for real dates.
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial >= 1)
        {
            try
            {
                value = DateTime.FromOADate(serial);
                return true;
            }
            catch (ArgumentException) { }
        }

        if (TryParseParts(text, out value)) return true;

        // A stock form writes a note beside the expiry date - "09/02/2027 (BS)", "31/12/2026 (7 ctn
        // 7 pcs)", and just as often without the brackets. The note follows the date, never precedes it,
        // so the leading three date parts are read and the rest is dropped. Text that does not start with
        // a readable date - a signature line, a product description - still fails.
        if (TryParseLeadingDate(text, out value)) return true;

        // Culture parsing closes the remaining gaps (ISO timestamps, "August 27, 2026"), but only when the
        // text states a four-digit year. Without that guard it happily reads "21 Agu" as the current year
        // and files an invoice into whichever reporting week the upload happens to run in.
        return StatesFourDigitYear(text) &&
            (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value) ||
             DateTime.TryParse(text, Indonesian, DateTimeStyles.AllowWhiteSpaces, out value));
    }

    /// <summary>
    /// Collapses the whitespace variants exporters emit — non-breaking, thin, figure and zero-width
    /// spaces — into ordinary single spaces, so the rest of the parsing sees the text a reader sees.
    /// </summary>
    private static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;

        var builder = new System.Text.StringBuilder(source.Length);
        var pendingSpace = false;
        foreach (var character in source)
        {
            if (character is '\u200B' or '\u200C' or '\u200D' or '\uFEFF') continue;
            if (char.IsWhiteSpace(character) || character is '\u00A0' or '\u2007' or '\u2009' or '\u202F')
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Splits "21 Agu 2026", "27-Aug-26", "27/08/2026" and "2026-08-27" into day, month and year on any
    /// common separator.
    /// </summary>
    private static bool TryParseParts(string text, out DateTime value) => TryParseTokens(SplitParts(text), out value);

    /// <summary>
    /// Reads the date a text cell opens with and ignores whatever the writer appended to it. Only the
    /// leading three parts are considered, so a note has to follow the date to be dropped; text that
    /// merely contains a date somewhere is not mined for one.
    /// </summary>
    private static bool TryParseLeadingDate(string text, out DateTime value)
    {
        value = default;
        var parts = SplitParts(text);
        return parts.Count > 3 && TryParseTokens(parts.GetRange(0, 3), out value);
    }

    /// <summary>A trailing time of day is ignored: the canonical fields are dates.</summary>
    private static List<string> SplitParts(string text) => text
        .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .TakeWhile(part => !part.Contains(':', StringComparison.Ordinal))
        .ToList();

    private static bool TryParseTokens(List<string> parts, out DateTime value)
    {
        value = default;
        if (parts.Count != 3) return false;

        var monthIndex = parts.FindIndex(part => MonthNames.ContainsKey(part));
        if (monthIndex >= 0)
        {
            var month = MonthNames[parts[monthIndex]];
            var remaining = parts.Where((_, index) => index != monthIndex).ToList();
            // A named month leaves day and year, and only the year can exceed 31.
            return monthIndex == 0
                ? TryBuild(remaining[1], month, remaining[0], out value)
                : TryBuild(remaining[^1], month, remaining[0], out value);
        }

        if (parts.Any(part => !part.All(char.IsAsciiDigit))) return false;

        // Year-first is unambiguous; everything else is read day-first, as Indonesian workbooks are
        // written, and only falls back to month-first when the day-first reading is not a real date.
        return parts[0].Length == 4
            ? TryBuild(parts[0], parts[1], parts[2], out value)
            : TryBuild(parts[2], parts[1], parts[0], out value) ||
              TryBuild(parts[2], parts[0], parts[1], out value);
    }

    private static bool StatesFourDigitYear(string text)
    {
        var run = 0;
        foreach (var character in text)
        {
            if (char.IsAsciiDigit(character))
            {
                if (++run == 4) return true;
                continue;
            }

            run = 0;
        }

        return false;
    }

    private static bool TryBuild(string year, string month, string day, out DateTime value) =>
        TryBuild(year, int.TryParse(month, out var number) ? number : 0, day, out value);

    private static bool TryBuild(string year, int month, string day, out DateTime value)
    {
        value = default;
        if (!int.TryParse(year, out var yearNumber) || !int.TryParse(day, out var dayNumber)) return false;
        if (year.Length <= 2) yearNumber += yearNumber <= TwoDigitYearPivot ? 2000 : 1900;
        if (yearNumber < 1900 || yearNumber > 2999 || month is < 1 or > 12 || dayNumber is < 1 or > 31) return false;

        try
        {
            value = new DateTime(yearNumber, month, dayNumber);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
