using ClosedXML.Excel;
using GiselX.Service;

namespace GiselX.Tests;

public sealed class ItemMappingWorkbookParserTests
{
    [Fact]
    public void Parse_reports_duplicate_normalized_distributor_ids()
    {
        var bytes = Workbook(
            ("4100039", "Cream Caramel", " P0003 ", "Caramel Mix"),
            ("4100040", "Other", "p0003", "Other Name"));

        var result = new ItemMappingWorkbookParser().Parse(bytes);

        Assert.Equal(2, result.InvalidRows);
        Assert.All(result.Errors, error => Assert.Contains("duplicate", error.Reason,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_requires_the_exact_four_headers()
    {
        var bytes = WorkbookWithHeaders("GMK Item ID", "GMK Item Name", "Distributor Item ID", "Name");

        var result = new ItemMappingWorkbookParser().Parse(bytes);

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.SourceRowNumber);
        Assert.Contains("Distributor Item Name", error.Reason);
    }

    [Fact]
    public void Parse_trims_values_and_ignores_fully_blank_trailing_rows()
    {
        var bytes = Workbook((" 4100039 ", " Cream Caramel ", " P0003 ", " Caramel Mix "));

        var result = new ItemMappingWorkbookParser().Parse(bytes);

        Assert.Equal(1, result.ValidRows);
        var mapping = Assert.Single(result.Rows);
        Assert.Equal("4100039", mapping.GmkItemId);
        Assert.Equal("Cream Caramel", mapping.GmkItemName);
        Assert.Equal("P0003", mapping.DistributorItemId);
        Assert.Equal("Caramel Mix", mapping.DistributorItemName);
    }

    [Fact]
    public void Parse_reports_missing_and_overlength_values_at_the_source_row()
    {
        var bytes = Workbook(("", new string('G', 201), "P0003", "Caramel Mix"));

        var result = new ItemMappingWorkbookParser().Parse(bytes);

        Assert.Equal(1, result.InvalidRows);
        Assert.All(result.Errors, error => Assert.Equal(2, error.SourceRowNumber));
        Assert.Contains(result.Errors, error => error.Field == "GMK Item ID" && error.Reason.Contains("required"));
        Assert.Contains(result.Errors, error => error.Field == "GMK Item Name" && error.Reason.Contains("maximum length"));
    }

    private static byte[] Workbook(params (string GmkId, string GmkName, string DistributorId, string DistributorName)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Mappings");
        sheet.Cell(1, 1).Value = "GMK Item ID";
        sheet.Cell(1, 2).Value = "GMK Item Name";
        sheet.Cell(1, 3).Value = "Distributor Item ID";
        sheet.Cell(1, 4).Value = "Distributor Item Name";
        for (var index = 0; index < rows.Length; index++)
        {
            sheet.Cell(index + 2, 1).Value = rows[index].GmkId;
            sheet.Cell(index + 2, 2).Value = rows[index].GmkName;
            sheet.Cell(index + 2, 3).Value = rows[index].DistributorId;
            sheet.Cell(index + 2, 4).Value = rows[index].DistributorName;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] WorkbookWithHeaders(params string[] headers)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Mappings");
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
