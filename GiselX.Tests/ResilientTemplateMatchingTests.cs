using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

/// <summary>
/// Detection matches a mapping by its stored header signature first, and only when nothing matched that
/// way resolves each mapped column by its own header instead. These tests pin both halves: the rescue
/// accepts the drift distributors actually produce, and it never displaces an exact match.
/// </summary>
public sealed class ResilientTemplateMatchingTests
{
    [Fact]
    public async Task Sellout_matches_a_workbook_that_gained_an_unmapped_column()
    {
        // The signature records two columns; the file now carries a third the mapping never mapped.
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            (1, 1, "Invoice No"), (1, 2, "Customer ID"), (1, 3, "Notes"));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [TwoColumnMapping()]);

        Assert.True(result.IsMatch);
        Assert.Equal(1, ColumnOf(result.Mapping!, SelloutCanonicalField.InvoiceNo));
        Assert.Equal(2, ColumnOf(result.Mapping!, SelloutCanonicalField.CustomerId));
    }

    [Fact]
    public async Task Sellout_matches_reordered_columns_and_points_the_parser_at_their_new_positions()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            (1, 1, "Customer ID"), (1, 2, "Invoice No"));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [TwoColumnMapping()]);

        Assert.True(result.IsMatch);
        // The parser reads through these numbers, so they are what proves the right cells are read.
        Assert.Equal(2, ColumnOf(result.Mapping!, SelloutCanonicalField.InvoiceNo));
        Assert.Equal(1, ColumnOf(result.Mapping!, SelloutCanonicalField.CustomerId));
    }

    [Fact]
    public async Task Sellout_rejects_a_workbook_missing_a_mapped_header_and_names_that_header()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            (1, 1, "Invoice No"), (1, 2, "Customer Code"));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [TwoColumnMapping()]);

        Assert.False(result.IsMatch);
        Assert.Contains(result.Errors, error => error.Contains("Customer ID", StringComparison.Ordinal)
            && error.Contains("was not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sellout_prefers_an_exact_match_over_a_mapping_that_only_resolves_by_header()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            (1, 1, "Invoice No"), (1, 2, "Customer ID"));
        var exact = TwoColumnMapping(id: 1, name: "exact");
        // Same two columns, but a signature claiming a third: it can only ever match by header.
        var looser = TwoColumnMapping(id: 2, name: "looser",
            signature: "INVOICE NO\u001FCUSTOMER ID\u001FEXTRA");

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [looser, exact]);

        Assert.True(result.IsMatch);
        Assert.Equal(1, result.Mapping!.Id);
    }

    [Fact]
    public async Task Sellout_reports_ambiguity_when_two_mappings_resolve_by_header()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            (1, 1, "Invoice No"), (1, 2, "Customer ID"));
        var first = TwoColumnMapping(id: 1, name: "First", signature: "INVOICE NO\u001FCUSTOMER ID\u001FA");
        var second = TwoColumnMapping(id: 2, name: "Second", signature: "INVOICE NO\u001FCUSTOMER ID\u001FB");

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [first, second]);

        Assert.False(result.IsMatch);
        Assert.Contains(result.Errors, error => error.Contains("First", StringComparison.Ordinal)
            && error.Contains("Second", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stock_matches_a_worksheet_that_gained_an_unmapped_column()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            (1, 1, "Product ID"), (1, 2, "Saldo Akhir"), (1, 3, "Notes"));

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook, [StockMapping()]);

        Assert.True(result.IsMatch);
        var match = Assert.Single(result.Matches);
        Assert.Equal("Stock", match.WorksheetName);
    }

    [Fact]
    public async Task Stock_keeps_an_exactly_matched_worksheet_out_of_a_header_resolved_contest()
    {
        // Both mappings claim one worksheet. Before the rescue rule existed only the exact one matched and
        // the upload proceeded; the rescue must not turn that into an ambiguity error.
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            (1, 1, "Product ID"), (1, 2, "Saldo Akhir"));
        var exact = StockMapping(id: 1, name: "exact");
        var looser = StockMapping(id: 2, name: "looser",
            signature: "PRODUCT ID\u001FSALDO AKHIR\u001FEXTRA");

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook, [looser, exact]);

        Assert.True(result.IsMatch);
        Assert.Empty(result.Errors);
        Assert.Equal(1, Assert.Single(result.Matches).Mapping.Id);
    }

    private static int ColumnOf(DistributorTemplateMappingDto mapping, SelloutCanonicalField field) =>
        mapping.Columns.Single(column => column.CanonicalField == field).SourceColumnNumber;

    private static DistributorTemplateMappingDto TwoColumnMapping(
        int id = 1, string name = "mapping", string? signature = null) => new()
    {
        Id = id,
        Name = name,
        WorksheetName = "Data",
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        HeaderSignature = signature ?? "INVOICE NO\u001FCUSTOMER ID",
        IsActive = true,
        Columns =
        [
            new DistributorTemplateColumnDto
            {
                SourceColumnNumber = 1, SourceHeader = "Invoice No",
                CanonicalField = SelloutCanonicalField.InvoiceNo
            },
            new DistributorTemplateColumnDto
            {
                SourceColumnNumber = 2, SourceHeader = "Customer ID",
                CanonicalField = SelloutCanonicalField.CustomerId
            }
        ]
    };

    private static StockTemplateMappingDto StockMapping(
        int id = 1, string name = "stock mapping", string? signature = null) => new()
    {
        Id = id,
        Name = name,
        WorksheetName = "Stock",
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        HeaderSignature = signature ?? "PRODUCT ID\u001FSALDO AKHIR",
        IsActive = true,
        Columns =
        [
            new StockTemplateColumnDto
            {
                SourceColumnNumber = 1, SourceHeader = "Product ID",
                CanonicalField = StockCanonicalField.ProductId
            },
            new StockTemplateColumnDto
            {
                SourceColumnNumber = 2, SourceHeader = "Saldo Akhir",
                CanonicalField = StockCanonicalField.SaldoAkhir
            }
        ]
    };
}
