using ClosedXML.Excel;
using GiselX.Domain;
using GiselX.Service;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace GiselX.Tests;

public sealed class LegacyExcelWorkbookConverterTests
{
    [Fact]
    public void IsSupportedExtension_accepts_both_excel_extensions_and_rejects_anything_else()
    {
        Assert.True(LegacyExcelWorkbookConverter.IsSupportedExtension("sellout.xlsx"));
        Assert.True(LegacyExcelWorkbookConverter.IsSupportedExtension("sellout.xls"));
        Assert.True(LegacyExcelWorkbookConverter.IsSupportedExtension("SELLOUT.XLS"));
        Assert.False(LegacyExcelWorkbookConverter.IsSupportedExtension("sellout.csv"));
        Assert.False(LegacyExcelWorkbookConverter.IsSupportedExtension("sellout"));
        Assert.False(LegacyExcelWorkbookConverter.IsSupportedExtension(null));
    }

    [Fact]
    public void IsLegacyWorkbook_detects_by_content_not_by_file_name()
    {
        Assert.True(LegacyExcelWorkbookConverter.IsLegacyWorkbook(CreateLegacyWorkbook()));
        Assert.False(LegacyExcelWorkbookConverter.IsLegacyWorkbook(CreateOpenXmlWorkbook()));
        Assert.False(LegacyExcelWorkbookConverter.IsLegacyWorkbook([1, 2, 3]));
    }

    [Fact]
    public void EnsureOpenXml_returns_an_xlsx_workbook_untouched()
    {
        var workbook = CreateOpenXmlWorkbook();

        Assert.Same(workbook, LegacyExcelWorkbookConverter.EnsureOpenXml(workbook));
    }

    [Fact]
    public void EnsureOpenXml_leaves_unrecognised_bytes_for_the_callers_own_validation()
    {
        byte[] content = [1, 2, 3];

        Assert.Same(content, LegacyExcelWorkbookConverter.EnsureOpenXml(content));
    }

    [Fact]
    public void EnsureOpenXml_converts_a_legacy_workbook_into_readable_xlsx_bytes()
    {
        var converted = LegacyExcelWorkbookConverter.EnsureOpenXml(CreateLegacyWorkbook());

        Assert.True(LegacyExcelWorkbookConverter.IsOpenXmlWorkbook(converted));
        using var workbook = new XLWorkbook(new MemoryStream(converted));
        var worksheet = workbook.Worksheet("Sellout");
        Assert.Equal("Customer Id", worksheet.Cell(1, 1).GetString());
        Assert.Equal("Invoice Qty", worksheet.Cell(1, 3).GetString());
        Assert.Equal("C-001", worksheet.Cell(2, 1).GetString());
        Assert.Equal(12.5, worksheet.Cell(2, 3).GetDouble(), 3);
    }

    [Fact]
    public async Task Converted_workbook_is_readable_by_the_existing_sellout_inspector()
    {
        var converted = LegacyExcelWorkbookConverter.EnsureOpenXml(CreateLegacyWorkbook());

        var preview = await new XlsxSelloutWorkbookInspector()
            .InspectAsync(converted, "Sellout", headerRowNumber: 1, firstDataRowNumber: 2);

        Assert.Empty(preview.Errors);
        Assert.Equal(["Customer Id", "Customer Name", "Invoice Qty", "Invoice Date"], preview.Headers);
        Assert.Equal("C-001", preview.SampleRows[0][0]);
        Assert.Equal("Toko Maju", preview.SampleRows[0][1]);
        // Row 3 leaves the merged customer cells empty; the inspector fills them from the merge anchor.
        Assert.Equal("C-001", preview.SampleRows[1][0]);
        Assert.Equal("Toko Maju", preview.SampleRows[1][1]);
    }

    [Fact]
    public async Task Converted_dates_and_numbers_survive_the_canonical_cell_converter()
    {
        var converted = LegacyExcelWorkbookConverter.EnsureOpenXml(CreateLegacyWorkbook());

        var preview = await new XlsxSelloutWorkbookInspector()
            .InspectAsync(converted, "Sellout", headerRowNumber: 1, firstDataRowNumber: 2);

        // The inspector reads raw OOXML values, so a date arrives as an OLE serial the converter resolves.
        Assert.True(SelloutCellValueConverter.TryConvert(preview.SampleRows[0][3],
            SelloutCanonicalField.InvoiceDate, out var invoiceDate));
        Assert.Equal(new DateTime(2026, 8, 10), Assert.IsType<DateTime>(invoiceDate).Date);

        Assert.True(SelloutCellValueConverter.TryConvert(preview.SampleRows[0][2],
            SelloutCanonicalField.InvoiceQty, out var quantity));
        Assert.Equal(12.5m, Assert.IsType<decimal>(quantity));
    }

    [Fact]
    public void EnsureOpenXml_reports_a_actionable_error_for_an_unreadable_legacy_workbook()
    {
        // OLE2 header with no BIFF workbook stream behind it.
        var content = new byte[512];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(content, 0);

        var exception = Assert.Throws<InvalidDataException>(() => LegacyExcelWorkbookConverter.EnsureOpenXml(content));

        Assert.Contains(".xlsx", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureOpenXmlOrOriginal_falls_back_instead_of_throwing()
    {
        var content = new byte[512];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(content, 0);

        Assert.Same(content, LegacyExcelWorkbookConverter.EnsureOpenXmlOrOriginal(content));
    }

    [Fact]
    public void Converted_workbook_keeps_every_sheet_addressable_by_name()
    {
        var converted = LegacyExcelWorkbookConverter.EnsureOpenXml(CreateLegacyWorkbook());

        using var workbook = new XLWorkbook(new MemoryStream(converted));
        Assert.Equal(["Sellout", "Notes"], workbook.Worksheets.Select(sheet => sheet.Name));
    }

    private static byte[] CreateLegacyWorkbook()
    {
        using var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Sellout");
        var dateStyle = workbook.CreateCellStyle();
        dateStyle.DataFormat = workbook.CreateDataFormat().GetFormat("yyyy-mm-dd");

        var header = sheet.CreateRow(0);
        header.CreateCell(0).SetCellValue("Customer Id");
        header.CreateCell(1).SetCellValue("Customer Name");
        header.CreateCell(2).SetCellValue("Invoice Qty");
        header.CreateCell(3).SetCellValue("Invoice Date");

        var first = sheet.CreateRow(1);
        first.CreateCell(0).SetCellValue("C-001");
        first.CreateCell(1).SetCellValue("Toko Maju");
        first.CreateCell(2).SetCellValue(12.5d);
        var firstDate = first.CreateCell(3);
        firstDate.SetCellValue(new DateTime(2026, 8, 10));
        firstDate.CellStyle = dateStyle;

        var second = sheet.CreateRow(2);
        second.CreateCell(2).SetCellValue(4d);
        var secondDate = second.CreateCell(3);
        secondDate.SetCellValue(new DateTime(2026, 8, 11));
        secondDate.CellStyle = dateStyle;

        // Vertically merge the customer columns across both data rows, as distributor templates do.
        sheet.AddMergedRegion(new CellRangeAddress(1, 2, 0, 0));
        sheet.AddMergedRegion(new CellRangeAddress(1, 2, 1, 1));

        var notes = workbook.CreateSheet("Notes");
        notes.CreateRow(0).CreateCell(0).SetCellValue("Prepared by distributor");

        using var output = new MemoryStream();
        workbook.Write(output, leaveOpen: true);
        return output.ToArray();
    }

    private static byte[] CreateOpenXmlWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Sellout").Cell(1, 1).Value = "Customer Id";
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
