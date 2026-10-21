using System.Globalization;

namespace GiselX.Service;

/// <summary>
/// Turns one workbook cell into the value a canonical field expects. Both upload pipelines convert cells
/// the same way and only disagree on which field maps to which type, so the rules live here once and the
/// Stock and Sellout converters supply the type. Every fix to how a distributor writes a number or a date
/// then reaches both pipelines at once, which is what <see cref="WorkbookDateTextParser"/> already does
/// for dates.
/// </summary>
public static class WorkbookCellValueConverter
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public static bool TryConvert(string? source, Type targetType, out object? value)
    {
        var text = source?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            value = null;
            return true;
        }

        if (targetType == typeof(string))
        {
            value = text;
            return true;
        }

        // Exports write a dash into a numeric or date cell that has no value (an order that has not
        // shipped yet). Treat it as empty so an optional field stays null instead of failing the whole
        // row; a required field still reports that a value is required. Text fields keep it verbatim.
        if (IsDashPlaceholder(text))
        {
            value = null;
            return true;
        }

        if (targetType == typeof(int))
        {
            var valid = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
            value = valid ? number : null;
            return valid;
        }

        if (targetType == typeof(decimal))
        {
            // Excel writes a small number into the sheet XML in scientific notation ("1.6E-2" for 0.016),
            // so an exponent is accepted alongside plain and Indonesian-formatted numbers.
            const NumberStyles decimalStyles = NumberStyles.Number | NumberStyles.AllowExponent;
            var valid = decimal.TryParse(text, decimalStyles, CultureInfo.InvariantCulture, out var number)
                || decimal.TryParse(text, decimalStyles, Indonesian, out number);
            value = valid ? number : null;
            return valid;
        }

        // Serials, Indonesian and English month names, two-digit years and exporter whitespace all live
        // in one parser, shared by both pipelines.
        var dateValid = WorkbookDateTextParser.TryParse(text, out var date);
        value = dateValid ? date : null;
        return dateValid;
    }

    private static bool IsDashPlaceholder(string text) =>
        text.All(character => character is '-' or '\u2013' or '\u2014');
}
