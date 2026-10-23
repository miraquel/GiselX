using ClosedXML.Excel;

namespace GiselX.Common.Helpers;

public static class ExcelHelper
{
    /// <summary>Data rows read to size the export's columns; the header row is always included.</summary>
    private const int WidthSampleRows = 200;
    private const int MinColumnWidth = 8;
    private const int MaxColumnWidth = 60;

    public static IEnumerable<T> ParseExcel<T>(Stream stream, Func<IXLRow, T> mapRow, bool skipHeader = true)
    {
        var results = new List<T>();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var isFirstRow = true;
        foreach (var row in worksheet.RowsUsed())
        {
            if (skipHeader && isFirstRow)
            {
                isFirstRow = false;
                continue;
            }
            results.Add(mapRow(row));
        }

        return results;
    }
        
    public static Stream ExportExcel<T>(IEnumerable<T> data, Action<IXLRow, T> mapRow, string worksheetName = "Sheet1") where T : new()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(worksheetName);
        
        var headerInitialized = false;

        // Add data, create foreach loop with index
        var dataArray = data as T[] ?? data.ToArray();
        foreach (var row in dataArray.Select((item, index) => new { Index = index, Item = item }))
        {
            if (!headerInitialized)
            {
                mapRow(worksheet.Row(1), new T());
                headerInitialized = true;
            }
            
            var rowIndex = row.Index + 2;
            var xlRow = worksheet.Row(rowIndex);
            mapRow(xlRow, row.Item);
        }

        // Size columns from the text length of the header and the first rows. ClosedXML's AdjustToContents
        // measures every cell with font metrics, which made a 2,650-row Sellout download take seconds on the
        // server and grew with every row added.
        var columnCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastSampledRow = Math.Min(dataArray.Length + 1, WidthSampleRows + 1);
        for (var column = 1; column <= columnCount; column++)
        {
            var longest = 0;
            for (var rowNumber = 1; rowNumber <= lastSampledRow; rowNumber++)
                longest = Math.Max(longest, worksheet.Cell(rowNumber, column).GetFormattedString().Length);
            // +3 leaves room for the table's filter button in the header.
            worksheet.Column(column).Width = Math.Clamp(longest + 3, MinColumnWidth, MaxColumnWidth);
        }

        // Style header row with bold and background color, follow the range of columns used
        // var headerRow = worksheet.Range(1, 1, 1, worksheet.ColumnsUsed().Count());
        // headerRow.Style.Font.Bold = true;
        // headerRow.Style.Fill.BackgroundColor = XLColor.Blue;

        // Create table
        var range = worksheet.Range(1, 1, dataArray.Length + 1, worksheet.ColumnsUsed().Count());
        var table = range.CreateTable();
        table.ShowAutoFilter = true;

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public static Stream CreateExcelTemplate(Action<List<string>> mapHeader)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");

        var headers = new List<string>();
        mapHeader(headers);

        // Add headers
        for (var i = 0; i < headers.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Auto-adjust column widths
        worksheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}