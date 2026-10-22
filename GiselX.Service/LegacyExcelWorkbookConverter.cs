using System.Text;
using ClosedXML.Excel;
using ExcelDataReader;

namespace GiselX.Service;

/// <summary>
/// Converts legacy Excel 97-2003 (.xls / BIFF) uploads into the .xlsx bytes the rest of the
/// upload pipeline expects, so the workbook inspectors and parsers stay OOXML-only.
/// </summary>
public static class LegacyExcelWorkbookConverter
{
    public const string UnsupportedFileMessage = "Hanya file .xlsx dan .xls yang didukung.";
    public const string InvalidLegacyWorkbookMessage =
        "File tidak bisa dibaca sebagai Excel 97-2003 (.xls). Buka file di Excel, simpan sebagai .xlsx, lalu upload ulang.";

    private static readonly byte[] CompoundFileSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    static LegacyExcelWorkbookConverter() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static bool IsSupportedExtension(string? filename)
    {
        var extension = Path.GetExtension(filename ?? string.Empty);
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the bytes are an OLE2 compound document, which is how Excel 97-2003 stores a workbook.
    /// Detection is by content rather than file name, because distributors routinely rename one to the other.
    /// </summary>
    public static bool IsLegacyWorkbook(ReadOnlySpan<byte> content) => content.StartsWith(CompoundFileSignature);

    public static bool IsOpenXmlWorkbook(ReadOnlySpan<byte> content) => content.StartsWith(ZipSignature);

    /// <summary>
    /// Converts legacy .xls bytes to .xlsx and returns everything else unchanged, so callers keep
    /// reporting malformed uploads through their own workbook validation. Throws
    /// <see cref="InvalidDataException"/> only when the bytes are a legacy workbook that cannot be read.
    /// </summary>
    public static byte[] EnsureOpenXml(byte[] content)
    {
        if (!IsLegacyWorkbook(content)) return content;

        try
        {
            return Convert(content);
        }
        catch (Exception exception) when (exception is not InvalidDataException and not OutOfMemoryException)
        {
            throw new InvalidDataException(InvalidLegacyWorkbookMessage, exception);
        }
    }

    /// <summary>
    /// Converts a legacy workbook when it can, and otherwise hands back the original bytes so the
    /// caller's own workbook validation reports the failure in its usual shape.
    /// </summary>
    public static byte[] EnsureOpenXmlOrOriginal(byte[] content)
    {
        try
        {
            return EnsureOpenXml(content);
        }
        catch (InvalidDataException)
        {
            return content;
        }
    }

    private static byte[] Convert(byte[] content)
    {
        using var source = new MemoryStream(content, writable: false);
        using var reader = ExcelReaderFactory.CreateBinaryReader(source);
        using var workbook = new XLWorkbook();
        var sheetNumber = 0;

        do
        {
            sheetNumber++;
            var worksheet = workbook.Worksheets.Add(SheetName(reader.Name, sheetNumber, workbook));
            var rowNumber = 0;
            while (reader.Read())
            {
                rowNumber++;
                for (var field = 0; field < reader.FieldCount; field++)
                {
                    var value = reader.GetValue(field);
                    if (value is null) continue;
                    Write(worksheet.Cell(rowNumber, field + 1), value);
                }
            }

            foreach (var range in reader.MergeCells ?? [])
            {
                // ExcelDataReader reports merges zero-based; ClosedXML ranges are one-based.
                worksheet.Range(range.FromRow + 1, range.FromColumn + 1, range.ToRow + 1, range.ToColumn + 1)
                    .Merge();
            }
        } while (reader.NextResult());

        if (!workbook.Worksheets.Any()) throw new InvalidDataException(InvalidLegacyWorkbookMessage);

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static void Write(IXLCell cell, object value)
    {
        switch (value)
        {
            case string text:
                cell.Value = text;
                break;
            case DateTime date:
                cell.Value = date;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            case TimeSpan time:
                cell.Value = time;
                break;
            case bool flag:
                cell.Value = flag;
                break;
            case double number:
                cell.Value = number;
                break;
            case decimal number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            case long number:
                cell.Value = number;
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static string SheetName(string? name, int sheetNumber, XLWorkbook workbook)
    {
        var candidate = Sanitize(name);
        if (candidate.Length == 0) candidate = $"Sheet{sheetNumber}";
        if (!workbook.Worksheets.Contains(candidate)) return candidate;

        // A legacy workbook can carry names that collide once sanitised; keep every sheet addressable.
        var suffix = 2;
        var trimmed = candidate.Length > 28 ? candidate[..28] : candidate;
        while (workbook.Worksheets.Contains($"{trimmed} ({suffix})")) suffix++;
        return $"{trimmed} ({suffix})";
    }

    private static string Sanitize(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        foreach (var invalid in new[] { '\\', '/', '*', '?', ':', '[', ']' })
        {
            value = value.Replace(invalid, ' ');
        }
        value = value.Trim('\'').Trim();
        return value.Length > 31 ? value[..31] : value;
    }
}
