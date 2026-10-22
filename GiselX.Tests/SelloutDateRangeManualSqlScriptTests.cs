using System.Text.RegularExpressions;

namespace GiselX.Tests;

public sealed class SelloutDateRangeManualSqlScriptTests
{
    private const string Index = "IX_SelloutUploadBatch_CompanyId_ReportingWeekStart";

    [Fact]
    public void Script_is_transactional_locked_and_records_history()
    {
        var sql = ReadScript(out var migrationId);

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"N'{migrationId}'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_installs_both_procedures()
    {
        var sql = ReadScript(out _);

        Assert.Contains("CREATE OR ALTER PROCEDURE [dbo].[GiselSales_Select]", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectByCustPeriod]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_swaps_the_unique_filtered_index_for_a_plain_one_behind_guards()
    {
        var sql = ReadScript(out _);

        Assert.Matches(new Regex($@"IF\s+EXISTS[\s\S]*?DROP INDEX {Index}", RegexOptions.IgnoreCase), sql);
        var create = Regex.Match(sql, $@"CREATE INDEX {Index}[^;]*;", RegexOptions.IgnoreCase);
        Assert.True(create.Success);
        Assert.DoesNotContain("UNIQUE", create.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE", create.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE UNIQUE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_creates_the_invoice_date_index_behind_a_guard()
    {
        var sql = ReadScript(out _);

        Assert.Matches(new Regex(
            @"IF\s+NOT\s+EXISTS[\s\S]*?IX_SalesTransaction_CompanyId_InvoiceDate[\s\S]*?CREATE INDEX IX_SalesTransaction_CompanyId_InvoiceDate ON dbo\.SalesTransaction \(CompanyId, InvoiceDate\) INCLUDE \(SelloutUploadBatchId\);",
            RegexOptions.IgnoreCase), sql);
    }

    [Fact]
    public void Script_does_no_destructive_data_work()
    {
        var sql = ReadScript(out _);

        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadScript(out string migrationId)
    {
        var path = Assert.Single(Directory.GetFiles(
            FindFromSolutionRoot("GiselX.Web", "DatabaseScripts"), "*_SelloutDateRangeReplacement.sql"));
        migrationId = Path.GetFileNameWithoutExtension(path);
        return File.ReadAllText(path);
    }

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
