namespace GiselX.Tests;

public sealed class ItemMappingMigrationTests
{
    [Fact]
    public void Migration_backfills_distributor_identity_without_assigning_a_mapping()
    {
        var sql = ReadMigrationSql();

        Assert.Contains("DistributorProductId = ProductId", sql, StringComparison.Ordinal);
        Assert.Contains("DistributorProductName = ProductName", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SET ProductId = DistributorProductId", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_adds_the_company_switch_item_mapping_and_nullable_transaction_references()
    {
        var sql = ReadMigrationSql();

        Assert.Contains("ItemMappingEnabled", sql, StringComparison.Ordinal);
        Assert.Contains("defaultValue: false", sql, StringComparison.Ordinal);
        Assert.Contains("CreateTable", sql, StringComparison.Ordinal);
        Assert.Contains("ItemMapping", sql, StringComparison.Ordinal);
        Assert.Contains("GmkItemId", sql, StringComparison.Ordinal);
        Assert.Contains("DistributorItemId", sql, StringComparison.Ordinal);
        Assert.Contains("nvarchar(100)", sql, StringComparison.Ordinal);
        Assert.Contains("nvarchar(200)", sql, StringComparison.Ordinal);
        Assert.Contains("IX_ItemMapping_CompanyId_DistributorItemId", sql, StringComparison.Ordinal);
        Assert.Contains("onDelete: ReferentialAction.Restrict", sql, StringComparison.Ordinal);

        foreach (var table in new[] { "Stock", "StockStaging", "SalesTransaction", "SelloutStaging" })
        {
            Assert.Contains($"table: \"{table}\"", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Legacy_price_unit_upgrade_repairs_incompatible_columns_instead_of_throwing()
    {
        var sql = File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "Context", "Migrations", "20260810190000_AddStockPriceUnit.cs"));

        Assert.Contains("ALTER COLUMN PriceUnit decimal(18,2) NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("must be nullable decimal(18,2)", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMigrationSql() => File.ReadAllText(FindFromSolutionRoot(
        "GiselX.Web", "Context", "Migrations", "20260811180000_AddCompanyItemIdentityMapping.cs"));

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
