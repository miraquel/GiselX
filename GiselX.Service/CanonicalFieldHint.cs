namespace GiselX.Service;

/// <summary>
/// Builds the explanation an administrator sees when hovering a canonical field on a template mapping
/// screen. The format line is derived from the same type and length rules the workbook parsers enforce,
/// so the hint cannot promise something an upload would then reject.
/// </summary>
public static class CanonicalFieldHint
{
    public static string Compose(string description, Type valueType, int? maxLength, string? example)
    {
        var format = valueType == typeof(DateTime) ? "Tanggal"
            : valueType == typeof(decimal) || valueType == typeof(int) ? "Angka"
            : maxLength is { } limit ? $"Teks, maks. {limit} karakter"
            : "Teks";

        var lines = new List<string> { description, $"Format: {format}" };
        if (!string.IsNullOrWhiteSpace(example))
        {
            lines.Add($"Contoh: {example}");
        }

        return string.Join("\n", lines);
    }
}
