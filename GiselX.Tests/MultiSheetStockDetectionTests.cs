using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

/// <summary>
/// One Stock workbook is one snapshot, and it may carry several mapped worksheets. These pin which
/// combinations are accepted, which stay ambiguous, and what happens to worksheets nothing maps.
/// </summary>
public sealed class MultiSheetStockDetectionTests
{
    [Fact]
    public async Task Detection_accepts_two_mappings_on_two_different_worksheets()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Stok Gudang", [(1, 1, "Kode Barang")]),
            ("Stok Transit", [(1, 1, "Item Code")]));

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook,
            [Mapping(1, "gudang", "Stok Gudang", "KODE BARANG"), Mapping(2, "transit", "Stok Transit", "ITEM CODE")]);

        Assert.True(result.IsMatch);
        Assert.Empty(result.Errors);
        Assert.Equal(["Stok Gudang", "Stok Transit"], result.Matches.Select(match => match.WorksheetName));
        Assert.Equal(["gudang", "transit"], result.Matches.Select(match => match.Mapping.Name));
        Assert.Null(result.Mapping);
    }

    [Fact]
    public async Task Detection_accepts_two_worksheets_that_share_an_identical_header_layout()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Region A", [(1, 1, "Kode Barang")]),
            ("Region B", [(1, 1, "Kode Barang")]));

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook,
            [Mapping(1, "region a", "Region A", "KODE BARANG"), Mapping(2, "region b", "Region B", "KODE BARANG")]);

        Assert.True(result.IsMatch);
        Assert.Equal(2, result.Matches.Count);
    }

    [Fact]
    public async Task Detection_reports_worksheets_no_mapping_matched_without_failing()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Cover", [(1, 1, "Laporan Stok Bulanan")]),
            ("Stok Gudang", [(1, 1, "Kode Barang")]),
            ("Pivot", [(1, 1, "Total")]));

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook,
            [Mapping(1, "gudang", "Stok Gudang", "KODE BARANG")]);

        Assert.True(result.IsMatch);
        Assert.Equal("Stok Gudang", Assert.Single(result.Matches).WorksheetName);
        Assert.Equal(["Cover", "Pivot"], result.IgnoredWorksheetNames);
    }

    [Fact]
    public async Task Detection_still_rejects_two_mappings_claiming_the_same_worksheet()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Stok Gudang", [(1, 1, "Kode Barang")]));

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook,
            [Mapping(1, "gudang lama", "Stok Gudang", "KODE BARANG"),
             Mapping(2, "gudang baru", "Stok Gudang", "KODE BARANG")]);

        Assert.False(result.IsMatch);
        Assert.Empty(result.Matches);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Stok Gudang", error, StringComparison.Ordinal);
        Assert.Contains("gudang baru", error, StringComparison.Ordinal);
        Assert.Contains("gudang lama", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detection_still_names_the_reason_when_nothing_matches()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Stok Gudang", [(1, 1, "Kode Lain")]));

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook,
            [Mapping(1, "gudang", "Stok Gudang", "KODE BARANG")]);

        Assert.False(result.IsMatch);
        Assert.Empty(result.Matches);
        Assert.Contains("No active Stock mapping matches this workbook.", result.Errors);
        Assert.Contains(result.Errors, error => error.Contains("gudang", StringComparison.Ordinal)
            && error.Contains("KODE BARANG", StringComparison.Ordinal));
    }

    private static StockTemplateMappingDto Mapping(int id, string name, string worksheetName,
        params string[] normalizedHeaders) => new()
    {
        Id = id,
        Name = name,
        WorksheetName = worksheetName,
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        HeaderSignature = string.Join((char)0x1F, normalizedHeaders),
        IsActive = true,
        Columns = normalizedHeaders.Select((header, index) => new StockTemplateColumnDto
        {
            SourceColumnNumber = index + 1,
            SourceHeader = header,
            CanonicalField = StockCanonicalField.ProductId
        }).ToList()
    };
}
