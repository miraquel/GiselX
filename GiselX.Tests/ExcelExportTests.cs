using ClosedXML.Excel;
using GiselX.Common.Helpers;

namespace GiselX.Tests;

/// <summary>
/// The Sellout download took 8.5 seconds for 2,650 rows on the test server because the column widths
/// were measured over every cell. Widths now come from the header and a sample of the first rows, which
/// is what a reader sees when the file opens.
/// </summary>
public sealed class ExcelExportTests
{
    private sealed class Line
    {
        public string Name { get; init; } = "";
        public decimal Qty { get; init; }
    }

    private static void Map(IXLRow row, Line line)
    {
        if (row.RowNumber() == 1)
        {
            row.Cell(1).Value = "Name";
            row.Cell(2).Value = "Qty";
            return;
        }

        row.Cell(1).Value = line.Name;
        row.Cell(2).Value = line.Qty;
    }

    private static IXLWorksheet Export(IEnumerable<Line> lines, out XLWorkbook workbook)
    {
        var stream = ExcelHelper.ExportExcel(lines, Map);
        workbook = new XLWorkbook(stream);
        return workbook.Worksheet(1);
    }

    [Fact]
    public void Export_writes_the_header_every_row_and_a_filterable_table()
    {
        var sheet = Export(Enumerable.Range(1, 3).Select(i => new Line { Name = $"Toko {i}", Qty = i }), out var workbook);
        using (workbook)
        {
            Assert.Equal("Name", sheet.Cell(1, 1).GetString());
            Assert.Equal("Toko 3", sheet.Cell(4, 1).GetString());
            Assert.Equal(3m, sheet.Cell(4, 2).GetValue<decimal>());
            Assert.True(Assert.Single(sheet.Tables).ShowAutoFilter);
        }
    }

    [Fact]
    public void Export_widens_a_column_for_long_values_near_the_top()
    {
        var lines = new[] { new Line { Name = "Toko Sumber Rejeki Makmur Abadi Sentosa Jaya", Qty = 1 } };

        var sheet = Export(lines, out var workbook);
        using (workbook)
            Assert.True(sheet.Column(1).Width > sheet.Column(2).Width);
    }

    [Fact]
    public void Export_sizes_columns_from_a_sample_rather_than_every_row()
    {
        // A long value deep in a large export no longer costs a measurement of every cell above it.
        var lines = Enumerable.Range(1, 5000).Select(i => new Line { Name = "Toko", Qty = i })
            .Append(new Line { Name = new string('X', 120), Qty = 0 });

        var sheet = Export(lines, out var workbook);
        using (workbook)
            Assert.True(sheet.Column(1).Width < 30);
    }
}
