using GiselX.Web.Context.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

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

    [Fact]
    public void Migration_down_restores_pre_mapping_company_and_sales_procedure_contracts()
    {
        var sql = ReadMigrationSql();
        var down = sql[sql.IndexOf("protected override void Down", StringComparison.Ordinal)..];

        Assert.Contains("IsolateProcedureBatch(PreviousCompanyInsertProcedure)", down, StringComparison.Ordinal);
        Assert.Contains("IsolateProcedureBatch(PreviousCompanyUpdateProcedure)", down, StringComparison.Ordinal);
        Assert.Contains("IsolateProcedureBatch(PreviousCompanySelectByIdProcedure)", down, StringComparison.Ordinal);
        Assert.Contains("IsolateProcedureBatch(PreviousCompanySelectProcedure)", down, StringComparison.Ordinal);
        Assert.Contains("IsolateProcedureBatch(PreviousSalesSelectProcedure)", down, StringComparison.Ordinal);
        Assert.Contains("PreviousCompanyInsertProcedure", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@ItemMappingEnabled", PreviousProcedure(sql, "PreviousCompanyInsertProcedure"), StringComparison.Ordinal);
        Assert.DoesNotContain("@UseDistributorIdentity", PreviousProcedure(sql, "PreviousSalesSelectProcedure"), StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_does_not_change_the_preexisting_identity_schema()
    {
        var sql = ReadMigrationSql();

        Assert.DoesNotContain("AspNetUserLogins", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetUserTokens", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderKey", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Procedure_definitions_are_isolated_in_dynamic_sql_batches()
    {
        var migration = new InspectableItemMappingMigration();
        var procedureOperations = migration.GetUpOperations()
            .Concat(migration.GetDownOperations())
            .OfType<SqlOperation>()
            .Where(operation => operation.Sql.Contains("CREATE OR ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(procedureOperations);
        Assert.All(procedureOperations, operation =>
        {
            var sql = operation.Sql.Trim();
            Assert.StartsWith("EXEC(N'", sql, StringComparison.Ordinal);
            Assert.EndsWith("');", sql, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Upgrade_installs_every_required_mapping_procedure()
    {
        var migration = new InspectableItemMappingMigration();
        var sql = string.Join("\n", migration.GetUpOperations()
            .OfType<SqlOperation>()
            .Select(operation => operation.Sql));

        foreach (var procedure in new[]
                 {
                     "Company_Insert",
                     "Company_Update",
                     "Company_SelectById",
                     "Company_Select",
                     "GiselSales_Select",
                     "GiselSales_SelectByCustPeriod"
                 })
        {
            Assert.Contains($"[dbo].[{procedure}]", sql, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("tokens truncated", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMigrationSql() => File.ReadAllText(FindFromSolutionRoot(
        "GiselX.Web", "Context", "Migrations", "20260811180000_AddCompanyItemIdentityMapping.cs"));

    private static string PreviousProcedure(string source, string name)
    {
        var start = source.IndexOf($"private const string {name} = \"\"\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {name}.");
        var end = source.IndexOf("\"\"\";", start, StringComparison.Ordinal);
        return source[start..end];
    }

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }

    private sealed class InspectableItemMappingMigration : AddCompanyItemIdentityMapping
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> GetDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Down(builder);
            return builder.Operations;
        }
    }
}
