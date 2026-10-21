using System.Text.RegularExpressions;

namespace GiselX.Tests;

public sealed class StockAreaManualSqlScriptTests
{
    private static readonly (string Table, string Column)[] NewColumns =
    [
        ("Stock", "StockAreaId"),
        ("Stock", "Condition"),
        ("StockStaging", "StockAreaId"),
        ("StockStaging", "Condition"),
        ("StockStaging", "SourceAreaText"),
        ("StockStaging", "SourceConditionText"),
        ("StockTemplateMapping", "GoodConditionValues"),
        ("StockTemplateMapping", "BadConditionValues")
    ];

    [Fact]
    public void Script_contains_transaction_and_rerun_guards()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.StockArea', N'U') IS NULL", sql, StringComparison.OrdinalIgnoreCase);

        foreach (var (table, column) in NewColumns)
        {
            Assert.Contains($"COL_LENGTH(N'dbo.{table}', N'{column}')", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Script_defaults_both_condition_columns_to_good()
    {
        var sql = Regex.Replace(ReadScript(), @"\s+", " ");

        Assert.Equal(2, Regex.Matches(sql, @"Condition tinyint NOT NULL\s+(?:CONSTRAINT \w+ )?DEFAULT \(0\)", RegexOptions.IgnoreCase).Count);
    }

    [Fact]
    public void Script_has_no_destructive_statement()
    {
        var sql = ReadScript();

        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_records_the_manually_applied_ef_migration()
    {
        var sql = ReadScript();
        var migrationId = Path.GetFileNameWithoutExtension(FindScript()!);

        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"N'{migrationId}'", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), "GiselX.Web", "Context", "Migrations", migrationId + ".cs")),
            "The script's migration id must name an existing migration.");
    }

    private static string ReadScript()
    {
        var path = FindScript();
        Assert.True(path is not null, "Expected DatabaseScripts/*_AddStockAreaAndCondition.sql.");
        return File.ReadAllText(path!);
    }

    private static string? FindScript() => Directory
        .GetFiles(Path.Combine(FindRepositoryRoot(), "GiselX.Web", "DatabaseScripts"), "*_AddStockAreaAndCondition.sql")
        .SingleOrDefault();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate GiselX.sln.");
    }
}
