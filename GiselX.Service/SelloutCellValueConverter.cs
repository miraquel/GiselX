using System.Globalization;
using GiselX.Domain;

namespace GiselX.Service;

public static class SelloutCellValueConverter
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public static bool TryConvert(string? source, SelloutCanonicalField field, out object? value)
    {
        var text = source?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            value = null;
            return true;
        }

        var type = SelloutCanonicalFieldCatalog.GetValueType(field);
        if (type == typeof(string))
        {
            value = text;
            return true;
        }
        if (type == typeof(int))
        {
            var valid = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
            value = valid ? number : null;
            return valid;
        }
        if (type == typeof(decimal))
        {
            var valid = decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                || decimal.TryParse(text, NumberStyles.Number, Indonesian, out number);
            value = valid ? number : null;
            return valid;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial >= 1)
        {
            try
            {
                value = DateTime.FromOADate(serial);
                return true;
            }
            catch (ArgumentException) { }
        }
        var dateValid = DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            || DateTime.TryParse(text, Indonesian, DateTimeStyles.AllowWhiteSpaces, out date);
        value = dateValid ? date : null;
        return dateValid;
    }
}
