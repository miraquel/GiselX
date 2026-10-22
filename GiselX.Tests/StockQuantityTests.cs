using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

/// <summary>
/// The Stock page showed Saldo Awal, Masuk PO and Saldo Akhir, which PT. GMK found confusing: what they
/// need is the quantity on hand, in cartons and pieces. Distributors' files carry it either as one column
/// or as two (cartons and pieces), so a template maps QtyCarton, QtyPcs or both - at least one of them.
/// </summary>
public sealed class StockQuantityTests
{
    [Fact]
    public void Quantity_fields_are_appended_so_stored_field_numbers_do_not_shift()
    {
        Assert.Equal(16, (int)StockCanonicalField.QtyCarton);
        Assert.Equal(17, (int)StockCanonicalField.QtyPcs);
    }

    [Fact]
    public void Only_product_identity_is_individually_required()
    {
        Assert.Equal(new HashSet<StockCanonicalField> { StockCanonicalField.ProductId, StockCanonicalField.ProductName },
            StockCanonicalFieldCatalog.RequiredFields);
    }

    [Fact]
    public void The_template_editor_offers_the_quantities_and_no_longer_the_saldo_fields()
    {
        var offered = StockCanonicalFieldCatalog.OfferedFields;

        Assert.Contains(StockCanonicalField.QtyCarton, offered);
        Assert.Contains(StockCanonicalField.QtyPcs, offered);
        Assert.DoesNotContain(StockCanonicalField.SaldoAwal, offered);
        Assert.DoesNotContain(StockCanonicalField.SaldoMasukPO, offered);
        Assert.DoesNotContain(StockCanonicalField.SaldoAkhir, offered);
    }

    [Theory]
    [InlineData(StockCanonicalField.QtyCarton)]
    [InlineData(StockCanonicalField.QtyPcs)]
    public void Both_quantities_are_numbers(StockCanonicalField field) =>
        Assert.Equal(typeof(decimal), StockCanonicalFieldCatalog.GetValueType(field));

    [Fact]
    public void Model_stores_both_quantities_as_optional_decimals_on_stock_and_staging()
    {
        using var db = new GiselXDbContext(new DbContextOptionsBuilder<GiselXDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXStockQuantityTest;Trusted_Connection=True")
            .Options);

        foreach (var entity in new[] { typeof(Stock), typeof(StockStaging) })
        foreach (var name in new[] { nameof(Stock.QtyCarton), nameof(Stock.QtyPcs) })
        {
            var property = db.Model.FindEntityType(entity)!.FindProperty(name)!;
            Assert.Equal("decimal(18,4)", property.GetColumnType()!.Replace(" ", ""));
            Assert.True(property.IsNullable);
        }
    }

    [Fact]
    public void Manual_script_adds_all_four_columns_guarded_and_records_history()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "GiselX.sln"))) root = root.Parent!;
        var sql = File.ReadAllText(Path.Combine(root.FullName, "GiselX.Web", "DatabaseScripts",
            "20261022071507_AddStockQuantity.sql"));

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.Ordinal);
        Assert.Contains("sp_getapplock", sql, StringComparison.Ordinal);
        foreach (var (table, column) in new[] { ("Stock", "QtyCarton"), ("Stock", "QtyPcs"),
                     ("StockStaging", "QtyCarton"), ("StockStaging", "QtyPcs") })
        {
            Assert.Contains($"COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL", sql, StringComparison.Ordinal);
            Assert.Contains($"ALTER TABLE dbo.{table} ADD {column} decimal(18,4) NULL", sql, StringComparison.Ordinal);
        }
        Assert.Contains("VALUES (N'20261022071507_AddStockQuantity', N'10.0.8')", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Publication_copies_both_quantities_and_tolerates_an_unmapped_closing_balance()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "GiselX.sln"))) root = root.Parent!;
        var source = File.ReadAllText(Path.Combine(root.FullName, "GiselX.Repository", "StockUploadRepository.cs"));

        Assert.Contains("QtyCarton, QtyPcs, BatchNumber, ExpiredDate,", source, StringComparison.Ordinal);
        Assert.Contains("COALESCE(s.SaldoAkhir,0),", source, StringComparison.Ordinal);
        Assert.Contains("s.QtyCarton, s.QtyPcs,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_reads_cartons_and_pieces_from_two_columns()
    {
        var row = Assert.Single(Parse(
            [(3, 1, "P1"), (3, 2, "Product 1"), (3, 3, "12"), (3, 4, "5")],
            StockCanonicalField.QtyCarton, StockCanonicalField.QtyPcs).Rows);

        Assert.True(row.IsValid);
        Assert.Equal(12m, row.QtyCarton);
        Assert.Equal(5m, row.QtyPcs);
    }

    [Fact]
    public void Parse_reads_a_single_quantity_column_and_a_blank_cell_as_zero()
    {
        var rows = Parse([(3, 1, "P1"), (3, 2, "Product 1"), (3, 3, "40"), (4, 1, "P2"), (4, 2, "Product 2")],
            StockCanonicalField.QtyPcs).Rows;

        Assert.Equal([40m, 0m], rows.Select(row => row.QtyPcs));
        Assert.All(rows, row => Assert.Null(row.QtyCarton));
    }

    [Fact]
    public void Parse_refuses_a_negative_quantity()
    {
        var row = Assert.Single(Parse([(3, 1, "P1"), (3, 2, "Product 1"), (3, 3, "-2")],
            StockCanonicalField.QtyCarton).Rows);

        Assert.False(row.IsValid);
        Assert.Contains("QtyCarton must not be negative.", row.ValidationErrorsJson);
    }

    private static StockParseResult Parse((int Row, int Column, string Value)[] cells,
        params StockCanonicalField[] quantityFields)
    {
        var columns = new List<StockTemplateColumnDto>
        {
            new() { SourceColumnNumber = 1, SourceHeader = "Product ID", CanonicalField = StockCanonicalField.ProductId },
            new() { SourceColumnNumber = 2, SourceHeader = "Product Name", CanonicalField = StockCanonicalField.ProductName }
        };
        columns.AddRange(quantityFields.Select((field, index) => new StockTemplateColumnDto
            { SourceColumnNumber = 3 + index, SourceHeader = field.ToString(), CanonicalField = field }));
        var mapping = new StockTemplateMappingDto
        {
            Id = 1, WorksheetName = "Stock", HeaderRowNumber = 1, FirstDataRowNumber = 3, Columns = columns
        };
        return new StockWorkbookParser().Parse(
            ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock", cells), mapping);
    }
}
