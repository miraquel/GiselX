using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

/// <summary>
/// A workbook that matches no template used to report only that nothing matched, which hid the actual
/// cause (a worksheet name that drifted, a changed header). These pin the specific reason per template.
/// </summary>
public sealed class WorkbookTemplateDetectionReasonTests
{
    [Fact]
    public async Task Detection_says_so_when_the_company_has_no_active_template()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", (1, 1, "Invoice No"));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, []);

        var error = Assert.Single(result.Errors);
        Assert.Contains("no active distributor mapping is configured", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detection_names_the_template_and_the_missing_worksheet()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Sheet1", (1, 1, "Invoice No"));
        var mapping = Mapping("psb mapping", "Lap Penjualan", "INVOICE NO");

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [mapping]);

        Assert.False(result.IsMatch);
        Assert.Contains(result.Errors, error => error.Contains("psb mapping", StringComparison.Ordinal)
            && error.Contains("Lap Penjualan", StringComparison.Ordinal)
            && error.Contains("was not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Detection_reports_the_column_whose_header_changed()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            (1, 1, "Invoice No"), (1, 2, "Customer Code"));
        var mapping = Mapping("csu mapping", "Data", "INVOICE NO", "CUSTOMER ID");

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [mapping]);

        Assert.False(result.IsMatch);
        Assert.Contains(result.Errors, error => error.Contains("csu mapping", StringComparison.Ordinal)
            && error.Contains("column 2", StringComparison.OrdinalIgnoreCase)
            && error.Contains("CUSTOMER CODE", StringComparison.Ordinal)
            && error.Contains("CUSTOMER ID", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detection_reports_a_column_count_difference()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", (1, 1, "Invoice No"));
        var mapping = Mapping("wide mapping", "Data", "INVOICE NO", "CUSTOMER ID", "PRODUCT ID");

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [mapping]);

        Assert.Contains(result.Errors, error => error.Contains("wide mapping", StringComparison.Ordinal)
            && error.Contains("1 column(s)", StringComparison.Ordinal)
            && error.Contains("expects 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detection_lists_the_templates_to_deactivate_when_several_match()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", (1, 1, "Invoice No"));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook,
            [Mapping("First", "Data", "INVOICE NO"), Mapping("Second", "Data", "INVOICE NO")]);

        Assert.False(result.IsMatch);
        var error = Assert.Single(result.Errors);
        Assert.Contains("First", error, StringComparison.Ordinal);
        Assert.Contains("Second", error, StringComparison.Ordinal);
        Assert.Contains("Deactivate", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detection_ignores_an_inactive_template_without_reporting_it()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", (1, 1, "Invoice No"));
        var mapping = Mapping("retired", "Nowhere", "INVOICE NO");
        mapping.IsActive = false;

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, [mapping]);

        Assert.DoesNotContain(result.Errors, error => error.Contains("retired", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stock_detection_also_names_the_template_and_the_missing_worksheet()
    {
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Sheet1", (1, 1, "Kode Barang"));
        var mapping = new StockTemplateMappingDto
        {
            Id = 1,
            Name = "stock mapping",
            WorksheetName = "Stok Bulanan",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 2,
            HeaderSignature = "KODE BARANG",
            IsActive = true,
            Columns = [new StockTemplateColumnDto
            {
                SourceColumnNumber = 1, SourceHeader = "Kode Barang", CanonicalField = StockCanonicalField.ProductId
            }]
        };

        var result = await new XlsxStockWorkbookInspector().DetectAsync(workbook, [mapping]);

        Assert.Null(result.Mapping);
        Assert.Contains(result.Errors, error => error.Contains("stock mapping", StringComparison.Ordinal)
            && error.Contains("Stok Bulanan", StringComparison.Ordinal));
    }

    /// <summary>
    /// Builds a mapping that maps one column per header its signature claims. A real mapping may map
    /// fewer columns than the header row holds — unmapped columns are ordinary — but a fixture that
    /// claims a header and then does not map it is asserting nothing about that header.
    /// </summary>
    private static DistributorTemplateMappingDto Mapping(string name, string worksheetName,
        params string[] normalizedHeaders) => new()
    {
        Id = 1,
        Name = name,
        WorksheetName = worksheetName,
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        HeaderSignature = string.Join('\u001F', normalizedHeaders),
        IsActive = true,
        Columns = normalizedHeaders.Select((header, index) => new DistributorTemplateColumnDto
        {
            SourceColumnNumber = index + 1,
            SourceHeader = header,
            CanonicalField = MappedFields[index]
        }).ToList()
    };

    private static readonly SelloutCanonicalField[] MappedFields =
    [
        SelloutCanonicalField.InvoiceNo, SelloutCanonicalField.CustomerId, SelloutCanonicalField.ProductId,
        SelloutCanonicalField.CustomerName, SelloutCanonicalField.ProductName, SelloutCanonicalField.InvoiceDate
    ];
}
