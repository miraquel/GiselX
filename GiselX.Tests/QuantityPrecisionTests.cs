using System.Text.RegularExpressions;
using GiselX.Domain;
using GiselX.Service;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

/// <summary>
/// A distributor workbook counts in fractions - a carton conversion of 9.6, an invoiced quantity of
/// 0.016 CTN that Excel stores as "1.6E-2". These pin the parsing and the storage shape that let such a
/// row through instead of failing it.
/// </summary>
public sealed class QuantityPrecisionTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXQuantityPrecisionTest;Trusted_Connection=True")
        .Options);

    [Theory]
    [InlineData("9.6", 9.6)]
    [InlineData("12.5", 12.5)]
    [InlineData("12", 12)]
    public void Sellout_reads_a_fractional_carton_conversion(string cell, double expected)
    {
        Assert.True(SelloutCellValueConverter.TryConvert(cell, SelloutCanonicalField.ProductPcsInCtn, out var value));
        Assert.Equal((decimal)expected, Assert.IsType<decimal>(value));
    }

    [Theory]
    [InlineData("9.6", 9.6)]
    [InlineData("12.5", 12.5)]
    public void Stock_reads_a_fractional_carton_conversion(string cell, double expected)
    {
        Assert.True(StockCellValueConverter.TryConvert(cell, StockCanonicalField.ProductPcsInCtn, out var value));
        Assert.Equal((decimal)expected, Assert.IsType<decimal>(value));
    }

    [Theory]
    [InlineData("1.6E-2", 0.016)]
    [InlineData("1.6e-2", 0.016)]
    [InlineData("2.5E+2", 250)]
    public void Sellout_reads_a_quantity_written_in_scientific_notation(string cell, double expected)
    {
        Assert.True(SelloutCellValueConverter.TryConvert(cell, SelloutCanonicalField.InvoiceQty, out var value));
        Assert.Equal((decimal)expected, Assert.IsType<decimal>(value));
    }

    [Fact]
    public void Stock_reads_a_balance_written_in_scientific_notation()
    {
        Assert.True(StockCellValueConverter.TryConvert("1.6E-2", StockCanonicalField.SaldoAkhir, out var value));
        Assert.Equal(0.016m, Assert.IsType<decimal>(value));
    }

    [Fact]
    public void Catalogs_type_the_carton_conversion_as_a_decimal()
    {
        Assert.Equal(typeof(decimal), SelloutCanonicalFieldCatalog.GetValueType(SelloutCanonicalField.ProductPcsInCtn));
        Assert.Equal(typeof(decimal), StockCanonicalFieldCatalog.GetValueType(StockCanonicalField.ProductPcsInCtn));
    }

    [Fact]
    public void Model_maps_the_carton_conversion_as_a_decimal_on_every_table()
    {
        using var db = CreateContext();

        foreach (var entity in new[]
                 {
                     db.Model.FindEntityType(typeof(Stock))!,
                     db.Model.FindEntityType(typeof(StockStaging))!,
                     db.Model.FindEntityType(typeof(SalesTransaction))!,
                     db.Model.FindEntityType(typeof(SelloutStaging))!
                 })
        {
            Assert.Equal("decimal(18,2)", ColumnType(entity, nameof(Stock.ProductPcsInCtn)));
        }
    }

    [Fact]
    public void Model_keeps_four_decimals_on_sold_quantities()
    {
        using var db = CreateContext();
        var sales = db.Model.FindEntityType(typeof(SalesTransaction))!;
        var staging = db.Model.FindEntityType(typeof(SelloutStaging))!;

        foreach (var entity in new[] { sales, staging })
        {
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(SalesTransaction.InvoiceQty)));
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(SalesTransaction.InKg)));

            // Money is unaffected: an invoice value is still counted to the cent.
            Assert.Equal("decimal(18,2)", ColumnType(entity, nameof(SalesTransaction.UnitPrice)));
            Assert.Equal("decimal(18,2)", ColumnType(entity, nameof(SalesTransaction.NetValue)));
        }
    }

    [Fact]
    public void Model_keeps_four_decimals_on_stock_balances()
    {
        using var db = CreateContext();
        var stock = db.Model.FindEntityType(typeof(Stock))!;
        var staging = db.Model.FindEntityType(typeof(StockStaging))!;

        foreach (var entity in new[] { stock, staging })
        {
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(Stock.ProductNetto)));
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(Stock.SmallestUnitQty)));
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(Stock.SaldoAwal)));
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(Stock.SaldoMasukPO)));
            Assert.Equal("decimal(18,4)", ColumnType(entity, nameof(Stock.SaldoAkhir)));

            Assert.Equal("decimal(18,2)", ColumnType(entity, nameof(Stock.PriceUnit)));
        }
    }

    [Fact]
    public void Script_alters_every_quantity_column_behind_a_shape_check()
    {
        var sql = ReadScript();

        foreach (var (table, column, type) in new[]
                 {
                     ("Stock", "ProductPcsInCtn", "decimal(18,2)"),
                     ("Stock", "ProductNetto", "decimal(18,4)"),
                     ("Stock", "SmallestUnitQty", "decimal(18,4)"),
                     ("Stock", "SaldoAwal", "decimal(18,4)"),
                     ("Stock", "SaldoMasukPO", "decimal(18,4)"),
                     ("Stock", "SaldoAkhir", "decimal(18,4)"),
                     ("StockStaging", "ProductPcsInCtn", "decimal(18,2)"),
                     ("StockStaging", "SaldoAkhir", "decimal(18,4)"),
                     ("SalesTransaction", "ProductPcsInCtn", "decimal(18,2)"),
                     ("SalesTransaction", "InvoiceQty", "decimal(18,4)"),
                     ("SalesTransaction", "InKg", "decimal(18,4)"),
                     ("SelloutStaging", "ProductPcsInCtn", "decimal(18,2)"),
                     ("SelloutStaging", "InvoiceQty", "decimal(18,4)"),
                     ("SelloutStaging", "InKg", "decimal(18,4)")
                 })
        {
            Assert.Contains($"ALTER TABLE dbo.{table} ALTER COLUMN {column} {type}", sql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("TYPE_NAME", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("c.precision <> 18", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("c.scale <> 4", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("c.is_nullable", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_rebuilds_the_paging_procedures_with_decimal_filters()
    {
        var sql = ReadScript();

        Assert.Contains("CREATE OR ALTER PROCEDURE [dbo].[GiselSales_Select]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE OR ALTER PROCEDURE [dbo].[GiselStock_Select]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ProductPcsInCtn    DECIMAL(18, 2) = NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@ProductPcsInCtn    INT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_guards_reruns_and_records_history_inside_the_transaction()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.Stock', N'U') IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.SalesTransaction', N'U') IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THROW", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20260911023244_WidenQuantityPrecision", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.True(sql.IndexOf("INSERT INTO dbo.__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) <
                    sql.IndexOf("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Script_has_no_destructive_data_statement()
    {
        var compact = Regex.Replace(ReadScript(), @"\s+", " ");

        Assert.DoesNotMatch(@"(?i)DELETE\s+(FROM\s+)?(?:dbo\.)?(Stock|SalesTransaction|SelloutStaging|StockStaging)\b", compact);
        Assert.DoesNotMatch(@"(?i)UPDATE\s+(?:dbo\.)?(Stock|SalesTransaction|SelloutStaging|StockStaging)\s+SET\b", compact);
        Assert.DoesNotMatch(@"(?i)TRUNCATE\s+TABLE\s+(?:dbo\.)?(Stock|SalesTransaction|SelloutStaging|StockStaging)\b", compact);
    }

    private static string ColumnType(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity, string property) =>
        entity.FindProperty(property)!.GetColumnType()!.Replace(" ", string.Empty);

    private static string ReadScript()
    {
        var scriptPath = Path.Combine(
            FindRepositoryRoot(),
            "GiselX.Web",
            "DatabaseScripts",
            "20260911023244_WidenQuantityPrecision.sql");

        Assert.True(File.Exists(scriptPath), $"Expected SQL deployment script at '{scriptPath}'.");
        return File.ReadAllText(scriptPath);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "GiselX.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate GiselX.sln above '{AppContext.BaseDirectory}'.");
    }
}
