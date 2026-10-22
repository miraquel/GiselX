using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class SelloutWorkbookParserTests
{
    [Fact]
    public void Parse_uses_configured_rows_and_ignores_blank_rows()
    {
        var result = Parse(CoreCells((6, "2026-08-09"), dataRow: 6), firstDataRow: 6);

        var row = Assert.Single(result.Rows);
        Assert.Equal(6, row.SourceRowNumber);
        Assert.True(row.IsValid);
        Assert.Null(result.BatchError);
    }

    [Fact]
    public void Parse_assigns_one_source_column_to_multiple_canonical_fields()
    {
        var mapping = Mapping(3);
        var customerName = mapping.Columns.Single(column =>
            column.CanonicalField == SelloutCanonicalField.CustomerName);
        customerName.SourceColumnNumber = 1;
        customerName.SourceHeader = "CustomerId";

        var result = new SelloutWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", CoreCells((3, "2026-08-03"))),
            mapping);

        var row = Assert.Single(result.Rows);
        Assert.Equal("C1", row.CustomerId);
        Assert.Equal("C1", row.CustomerName);
        Assert.True(row.IsValid);
    }

    [Fact]
    public void Parse_reports_missing_core_value_with_source_row()
    {
        var cells = CoreCells((7, "2026-08-03"), dataRow: 7)
            .Where(cell => cell.Column != 5).ToArray();

        var result = Parse(cells, firstDataRow: 7);

        var error = Assert.Single(result.Errors.Where(error => error.Field == SelloutCanonicalField.InvoiceNo));
        Assert.Equal(7, error.SourceRowNumber);
        Assert.Equal("Wajib diisi.", error.Reason);
    }

    [Fact]
    public void Parse_accepts_invoice_dates_without_a_selected_week()
    {
        var result = Parse(CoreCells((3, "2026-08-10")));

        Assert.DoesNotContain(result.Errors, error => error.Field == SelloutCanonicalField.InvoiceDate);
        Assert.True(result.Rows[0].IsValid);
    }

    [Fact]
    public void Parse_groups_rows_by_their_calculated_monday()
    {
        var cells = CoreCells((3, "2026-07-04"), dataRow: 3)
            .Concat(CoreCells((4, "2026-07-06"), dataRow: 4))
            .Concat(CoreCells((5, "2026-07-31"), dataRow: 5))
            .ToArray();

        var result = Parse(cells);

        Assert.Collection(result.WeekSummaries,
            week => Assert.Equal((new DateTime(2026, 6, 29), 1), (week.ReportingWeekStart, week.RowCount)),
            week => Assert.Equal((new DateTime(2026, 7, 6), 1), (week.ReportingWeekStart, week.RowCount)),
            week => Assert.Equal((new DateTime(2026, 7, 27), 1), (week.ReportingWeekStart, week.RowCount)));
    }

    [Fact]
    public void Parse_rejects_invalid_nonblank_optional_decimal()
    {
        var cells = CoreCells((3, "2026-08-03")).Append((3, 9, "not-money")).ToArray();
        var mapping = Mapping(3);
        mapping.Columns.Add(new DistributorTemplateColumnDto { SourceColumnNumber = 9, SourceHeader = "Unit Price", CanonicalField = SelloutCanonicalField.UnitPrice });

        var result = new SelloutWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", cells), mapping);

        Assert.Contains(result.Errors, error => error.Field == SelloutCanonicalField.UnitPrice);
    }

    [Fact]
    public void Parse_accepts_negative_invoice_quantity_for_returns()
    {
        var cells = CoreCells((3, "2026-08-03"))
            .Select(cell => cell.Column == 7 ? (cell.Row, cell.Column, "-2.5") : cell)
            .ToArray();

        var result = Parse(cells);

        Assert.True(Assert.Single(result.Rows).IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_rejects_zero_invoice_quantity()
    {
        var cells = CoreCells((3, "2026-08-03"))
            .Select(cell => cell.Column == 7 ? (cell.Row, cell.Column, "0") : cell)
            .ToArray();

        var result = Parse(cells);

        Assert.Contains(result.Errors, error => error.Field == SelloutCanonicalField.InvoiceQty);
    }

    [Fact]
    public void Parse_reports_batch_error_when_no_data_rows_exist()
    {
        var result = Parse([], firstDataRow: 3);

        Assert.Empty(result.Rows);
        Assert.NotNull(result.BatchError);
    }

    [Fact]
    public void Parse_ignores_and_reports_trailing_numeric_summary_rows()
    {
        var cells = CoreCells((3, "2026-08-03"))
            .Append((4, 7, "25"))
            .ToArray();

        var result = Parse(cells);

        Assert.Equal(3, Assert.Single(result.Rows).SourceRowNumber);
        Assert.Equal([4], result.IgnoredSummaryRowNumbers);
    }

    /// <summary>
    /// A caption typed across a quantity column - a signature line, a footnote - is not a total, and a
    /// row with no identity and no figure can never be published. It is ignored wherever it sits, so a
    /// backwards scan no longer stops short of the totals beneath it.
    /// </summary>
    [Fact]
    public void Parse_ignores_a_caption_row_between_the_data_and_the_totals()
    {
        var cells = CoreCells((3, "2026-08-03"))
            .Append((4, 7, "Mengetahui"))
            .Append((5, 7, "25"))
            .ToArray();

        var result = Parse(cells);

        Assert.Equal(3, Assert.Single(result.Rows).SourceRowNumber);
        Assert.Equal([4, 5], result.IgnoredSummaryRowNumbers);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_keeps_every_row_when_no_row_carries_an_identity()
    {
        var result = Parse([(3, 7, "Mengetahui"), (4, 7, "Disetujui")]);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.False(row.IsValid));
        Assert.Empty(result.IgnoredSummaryRowNumbers);
    }

    [Fact]
    public void Parse_keeps_summary_shaped_middle_row_as_invalid_transaction()
    {
        var cells = CoreCells((3, "2026-08-03"), dataRow: 3)
            .Append((4, 7, "25"))
            .Concat(CoreCells((5, "2026-08-04"), dataRow: 5))
            .ToArray();

        var result = Parse(cells);

        Assert.Equal(3, result.Rows.Count);
        Assert.False(result.Rows.Single(row => row.SourceRowNumber == 4).IsValid);
        Assert.Empty(result.IgnoredSummaryRowNumbers);
    }

    [Fact]
    public void Parse_keeps_identity_bearing_trailing_row_as_invalid_transaction()
    {
        var cells = CoreCells((3, "2026-08-03"))
            .Append((4, 1, "C2"))
            .Append((4, 7, "25"))
            .ToArray();

        var result = Parse(cells);

        Assert.Equal(2, result.Rows.Count);
        Assert.False(result.Rows.Single(row => row.SourceRowNumber == 4).IsValid);
        Assert.Empty(result.IgnoredSummaryRowNumbers);
    }

    [Fact]
    public void Parse_fills_grouped_values_down_from_vertical_merged_cells()
    {
        var cells = CoreCells((3, "2026-08-03"), dataRow: 3)
            .Concat(
            [
                (4, 3, "P2"), (4, 4, "Second Product"), (4, 7, "3"), (4, 8, "CTN")
            ])
            .ToArray();
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbookWithMergedCells(
            "Data", cells, "A3:A4", "B3:B4", "E3:E4", "F3:F4");

        var result = new SelloutWorkbookParser().Parse(workbook, Mapping(3));

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.True(row.IsValid));
        var second = result.Rows.Single(row => row.SourceRowNumber == 4);
        Assert.Equal("C1", second.CustomerId);
        Assert.Equal("Customer", second.CustomerName);
        Assert.Equal("INV-1", second.InvoiceNo);
        Assert.Equal(new DateTime(2026, 8, 3), second.InvoiceDate);
    }

    [Fact]
    public void Parse_ignores_in_row_subtotal_that_merges_across_mapped_identity_columns()
    {
        var cells = CoreCells((3, "2026-08-03"), dataRow: 3)
            .Append((4, 2, "Total Customer"))
            .Append((4, 7, "2"))
            .Concat(CoreCells((5, "2026-08-04"), dataRow: 5))
            .ToArray();
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbookWithMergedCells(
            "Data", cells, "B4:F4");

        var result = new SelloutWorkbookParser().Parse(workbook, Mapping(3));

        Assert.Equal([3, 5], result.Rows.Select(row => row.SourceRowNumber));
        Assert.Equal([4], result.IgnoredSummaryRowNumbers);
    }

    private static SelloutParseResult Parse(
        (int Row, int Column, string Value)[] cells,
        int firstDataRow = 3) => new SelloutWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", cells),
            Mapping(firstDataRow));

    private static DistributorTemplateMappingDto Mapping(int firstDataRow) => new()
    {
        WorksheetName = "Data",
        HeaderRowNumber = 1,
        FirstDataRowNumber = firstDataRow,
        Columns =
        [
            Column(1, SelloutCanonicalField.CustomerId), Column(2, SelloutCanonicalField.CustomerName),
            Column(3, SelloutCanonicalField.ProductId), Column(4, SelloutCanonicalField.ProductName),
            Column(5, SelloutCanonicalField.InvoiceNo), Column(6, SelloutCanonicalField.InvoiceDate),
            Column(7, SelloutCanonicalField.InvoiceQty), Column(8, SelloutCanonicalField.InvoiceUnit)
        ]
    };

    private static DistributorTemplateColumnDto Column(int source, SelloutCanonicalField field) =>
        new() { SourceColumnNumber = source, SourceHeader = field.ToString(), CanonicalField = field };

    private static (int Row, int Column, string Value)[] CoreCells((int Row, string Date) invoiceDate, int dataRow = 3) =>
    [
        (dataRow, 1, "C1"), (dataRow, 2, "Customer"), (dataRow, 3, "P1"), (dataRow, 4, "Product"),
        (dataRow, 5, "INV-1"), (invoiceDate.Row, 6, invoiceDate.Date), (dataRow, 7, "2.5"), (dataRow, 8, "PCS")
    ];
}
