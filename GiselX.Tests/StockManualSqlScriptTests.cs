using System.Text.RegularExpressions;

namespace GiselX.Tests;

public sealed class StockManualSqlScriptTests
{
    [Fact]
    public void Script_contains_transaction_and_rerun_guards()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.StockUploadBatch'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COL_LENGTH(N'dbo.Stock', N'StockDate')", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_creates_the_approved_schema_contract()
    {
        var sql = ReadScript();

        foreach (var table in new[]
                 {
                     "StockTemplateMapping",
                     "StockTemplateColumn",
                     "StockUploadBatch",
                     "StockStaging"
                 })
        {
            Assert.Contains($"CREATE TABLE dbo.{table}", sql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("StockUploadBatchId bigint NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StockDate date NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_has_no_destructive_stock_data_statement()
    {
        var compact = Regex.Replace(ReadScript(), @"\s+", " ");

        Assert.DoesNotMatch(@"(?i)DELETE\s+(FROM\s+)?(?:dbo\.)?Stock\b", compact);
        Assert.DoesNotMatch(@"(?i)UPDATE\s+(?:dbo\.)?Stock\s+SET\b", compact);
        Assert.DoesNotMatch(@"(?i)TRUNCATE\s+TABLE\s+(?:dbo\.)?Stock\b", compact);
    }

    [Fact]
    public void Script_records_the_manually_applied_ef_migration()
    {
        var sql = ReadScript();

        Assert.Contains("dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20260810160000_AddStockSnapshotUploads", sql, StringComparison.Ordinal);
        Assert.Contains("10.0.8", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void PriceUnit_script_guards_and_validates_both_nullable_decimal_columns()
    {
        var sql = ReadPriceUnitScript();

        Assert.Contains("COL_LENGTH(N'dbo.Stock', N'PriceUnit')", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COL_LENGTH(N'dbo.StockStaging', N'PriceUnit')", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TYPE_NAME", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("precision", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scale", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_nullable", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THROW", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PriceUnit_script_records_history_only_inside_the_transaction()
    {
        var sql = ReadPriceUnitScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20260810190000_AddStockPriceUnit", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.True(sql.IndexOf("INSERT INTO dbo.__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) <
                    sql.IndexOf("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadScript() => ReadScript("20260810160000_AddStockSnapshotUploads.sql");

    private static string ReadPriceUnitScript() => ReadScript("20260810190000_AddStockPriceUnit.sql");

    private static string ReadScript(string fileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(
            repositoryRoot,
            "GiselX.Web",
            "DatabaseScripts",
            fileName);

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
