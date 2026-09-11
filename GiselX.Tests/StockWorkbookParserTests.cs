using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class StockWorkbookParserTests
{
    [Fact]
    public void Parse_assigns_one_source_column_to_multiple_canonical_fields()
    {
        var mapping = new StockTemplateMappingDto
        {
            WorksheetName = "Stock",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 3,
            Columns =
            [
                Column(1, StockCanonicalField.ProductId),
                Column(1, StockCanonicalField.ProductName),
                Column(3, StockCanonicalField.SaldoAkhir)
            ]
        };

        var result = new StockWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock", CoreRow(3, "4")), mapping);

        var row = Assert.Single(result.Rows);
        Assert.Equal("P-3", row.ProductId);
        Assert.Equal("P-3", row.ProductName);
        Assert.True(row.IsValid);
    }

    [Fact]
    public void Parse_accepts_zero_saldo_akhir_for_publication()
    {
        var result = Parse(CoreRow(3, "0"));

        Assert.True(Assert.Single(result.Rows).IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_reads_blank_saldo_akhir_as_zero_instead_of_rejecting_the_row()
    {
        var result = Parse(CoreRow(3, string.Empty));

        var row = Assert.Single(result.Rows);
        Assert.Equal(0m, row.SaldoAkhir);
        Assert.True(row.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_still_requires_product_identity_when_it_is_blank()
    {
        var result = Parse([(3, 2, "Product 3"), (3, 3, "4")]);

        var row = Assert.Single(result.Rows);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.ProductId &&
            error.Reason == "A value is required.");
    }

    [Fact]
    public void Parse_marks_negative_saldo_akhir_invalid_but_keeps_the_row()
    {
        var result = Parse(CoreRow(3, "-1"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(-1m, row.SaldoAkhir);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.SourceRowNumber == 3 &&
            error.Field == StockCanonicalField.SaldoAkhir &&
            error.Reason == "SaldoAkhir must not be negative.");
    }

    [Fact]
    public void Parse_marks_invalid_optional_date_invalid_without_fallback()
    {
        var result = Parse(CoreRow(3, "4").Append((3, 4, "not-a-date")).ToArray(), includeExpiredDate: true);

        Assert.False(Assert.Single(result.Rows).IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.ExpiredDate);
    }

    [Fact]
    public void Parse_ignores_and_reports_trailing_numeric_summary()
    {
        var result = Parse(CoreRow(3, "4").Append((4, 3, "4")).ToArray());

        Assert.Equal(3, Assert.Single(result.Rows).SourceRowNumber);
        Assert.Equal([4], result.IgnoredSummaryRowNumbers);
    }

    [Fact]
    public void Parse_keeps_summary_shaped_middle_row_as_invalid_data()
    {
        var cells = CoreRow(3, "4").Append((4, 3, "5")).Concat(CoreRow(5, "6")).ToArray();

        var result = Parse(cells);

        Assert.Equal(3, result.Rows.Count);
        Assert.False(result.Rows.Single(row => row.SourceRowNumber == 4).IsValid);
        Assert.Empty(result.IgnoredSummaryRowNumbers);
    }

    /// <summary>
    /// A stock form ends with a signature block - "Mengetahui" over a blank name - and the totals sit
    /// below it. A row with no product and no figure cannot be published whatever its position, so it is
    /// ignored rather than reported as a row missing its required values.
    /// </summary>
    [Fact]
    public void Parse_ignores_a_signature_row_between_the_data_and_the_totals()
    {
        var cells = CoreRow(3, "4").Append((4, 3, "Mengetahui")).Append((5, 3, "4")).ToArray();

        var result = Parse(cells);

        Assert.Equal(3, Assert.Single(result.Rows).SourceRowNumber);
        Assert.Equal([4, 5], result.IgnoredSummaryRowNumbers);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_ignores_a_caption_row_above_the_data()
    {
        var cells = new[] { (3, 3, "Mengetahui") }.Concat(CoreRow(4, "4")).ToArray();

        var result = Parse(cells);

        Assert.Equal(4, Assert.Single(result.Rows).SourceRowNumber);
        Assert.Equal([3], result.IgnoredSummaryRowNumbers);
    }

    /// <summary>
    /// When every row looks ignorable the mapping is wrong, not the sheet. Reporting each row tells the
    /// user that, where silently publishing nothing would not.
    /// </summary>
    [Fact]
    public void Parse_keeps_every_row_when_no_row_carries_an_identity()
    {
        var result = Parse([(3, 3, "Mengetahui"), (4, 3, "Disetujui")]);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.False(row.IsValid));
        Assert.Empty(result.IgnoredSummaryRowNumbers);
    }

    [Fact]
    public void Parse_reads_an_expiry_date_that_carries_a_handwritten_note()
    {
        var result = Parse(
            CoreRow(3, "4").Append((3, 4, "31/12/2026 (7 ctn 7 pcs)")).ToArray(),
            includeExpiredDate: true);

        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Equal(new DateTime(2026, 12, 31), row.ExpiredDate);
    }

    [Fact]
    public void Parse_keeps_identity_bearing_trailing_row_as_data()
    {
        var cells = CoreRow(3, "4").Append((4, 1, "P2")).Append((4, 3, "5")).ToArray();

        var result = Parse(cells);

        Assert.Equal(2, result.Rows.Count);
        Assert.False(result.Rows.Single(row => row.SourceRowNumber == 4).IsValid);
        Assert.Empty(result.IgnoredSummaryRowNumbers);
    }

    [Fact]
    public void Parse_accepts_blank_price_unit_as_null()
    {
        var result = ParseWithPriceUnit(CoreRow(3, "4"));

        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Null(row.PriceUnit);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("12.50", 12.50)]
    public void Parse_accepts_nonnegative_price_unit(string raw, decimal expected)
    {
        var result = ParseWithPriceUnit(CoreRow(3, "4").Append((3, 4, raw)).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Equal(expected, row.PriceUnit);
    }

    [Fact]
    public void Parse_rejects_negative_price_unit_but_keeps_the_row()
    {
        var result = ParseWithPriceUnit(CoreRow(3, "4").Append((3, 4, "-0.01")).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.Equal(-0.01m, row.PriceUnit);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.PriceUnit &&
            error.Reason == "PriceUnit must not be negative.");
    }

    [Fact]
    public void Parse_rejects_nonnumeric_price_unit_as_a_conversion_error()
    {
        var result = ParseWithPriceUnit(CoreRow(3, "4").Append((3, 4, "free")).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.Null(row.PriceUnit);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.PriceUnit &&
            error.RejectedValue == "free" && error.Reason.Contains("Decimal"));
    }

    private static StockParseResult Parse(
        (int Row, int Column, string Value)[] cells,
        bool includeExpiredDate = false)
    {
        var mapping = new StockTemplateMappingDto
        {
            WorksheetName = "Stock",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 3,
            Columns =
            [
                Column(1, StockCanonicalField.ProductId),
                Column(2, StockCanonicalField.ProductName),
                Column(3, StockCanonicalField.SaldoAkhir)
            ]
        };
        if (includeExpiredDate) mapping.Columns.Add(Column(4, StockCanonicalField.ExpiredDate));
        return new StockWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock", cells), mapping);
    }

    [Fact]
    public void Parse_accepts_blank_smallest_unit_fields_as_null()
    {
        var result = ParseWithSmallestUnit(CoreRow(3, "4"));

        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Null(row.SmallestUnit);
        Assert.Null(row.SmallestUnitQty);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("24", 24)]
    [InlineData("0.50", 0.50)]
    public void Parse_accepts_nonnegative_smallest_unit_qty(string raw, decimal expected)
    {
        var result = ParseWithSmallestUnit(CoreRow(3, "4")
            .Append((3, 4, "PCS")).Append((3, 5, raw)).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Equal("PCS", row.SmallestUnit);
        Assert.Equal(expected, row.SmallestUnitQty);
    }

    [Fact]
    public void Parse_rejects_negative_smallest_unit_qty_but_keeps_the_row()
    {
        var result = ParseWithSmallestUnit(CoreRow(3, "4").Append((3, 5, "-1")).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.Equal(-1m, row.SmallestUnitQty);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.SmallestUnitQty &&
            error.Reason == "SmallestUnitQty must not be negative.");
    }

    [Fact]
    public void Parse_rejects_nonnumeric_smallest_unit_qty_as_a_conversion_error()
    {
        var result = ParseWithSmallestUnit(CoreRow(3, "4").Append((3, 5, "many")).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.Null(row.SmallestUnitQty);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.SmallestUnitQty &&
            error.RejectedValue == "many" && error.Reason.Contains("Decimal"));
    }

    [Fact]
    public void Parse_rejects_smallest_unit_longer_than_fifty_characters()
    {
        var result = ParseWithSmallestUnit(CoreRow(3, "4").Append((3, 4, new string('U', 51))).ToArray());

        var row = Assert.Single(result.Rows);
        Assert.False(row.IsValid);
        Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.SmallestUnit &&
            error.Reason == "Value exceeds the maximum length of 50.");
    }

    private static StockParseResult ParseWithSmallestUnit((int Row, int Column, string Value)[] cells)
    {
        var mapping = new StockTemplateMappingDto
        {
            WorksheetName = "Stock",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 3,
            Columns =
            [
                Column(1, StockCanonicalField.ProductId),
                Column(2, StockCanonicalField.ProductName),
                Column(3, StockCanonicalField.SaldoAkhir),
                Column(4, StockCanonicalField.SmallestUnit),
                Column(5, StockCanonicalField.SmallestUnitQty)
            ]
        };
        return new StockWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock", cells), mapping);
    }

    private static StockParseResult ParseWithPriceUnit((int Row, int Column, string Value)[] cells)
    {
        var mapping = new StockTemplateMappingDto
        {
            WorksheetName = "Stock",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 3,
            Columns =
            [
                Column(1, StockCanonicalField.ProductId),
                Column(2, StockCanonicalField.ProductName),
                Column(3, StockCanonicalField.SaldoAkhir),
                Column(4, StockCanonicalField.PriceUnit)
            ]
        };
        return new StockWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock", cells), mapping);
    }

    private static StockTemplateColumnDto Column(int source, StockCanonicalField field) => new()
    {
        SourceColumnNumber = source,
        SourceHeader = field.ToString(),
        CanonicalField = field
    };

    private static (int Row, int Column, string Value)[] CoreRow(int row, string saldoAkhir) =>
    [
        (row, 1, $"P-{row}"), (row, 2, $"Product {row}"), (row, 3, saldoAkhir)
    ];
}
