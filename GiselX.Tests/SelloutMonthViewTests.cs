using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// The Sellout month view used to keep only the rows of the newest upload in the period (the grid, the
/// download and the tiles alike), so a second weekly file hid every earlier week of the same month.
/// Replacement now deletes the superseded rows when a week is re-published, so every row that exists
/// in SalesTransaction is live and the readers must not filter any further. No test executes SQL; these
/// pin the predicate's absence and that the readable procedure sources are what a migration installs.
/// </summary>
public sealed class SelloutMonthViewTests
{
    [Fact]
    public void Distributor_grid_procedure_keeps_every_row_of_the_month()
    {
        var sql = File.ReadAllText(FindFromSolutionRoot("GiselX.Web", "StoredProcedures", "GiselSales_Select.sql"));

        Assert.DoesNotContain("MAX([CreatedDate])", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Download_procedure_keeps_every_row_of_the_month()
    {
        var sql = File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "StoredProcedures", "GiselSales_SelectByCustPeriod.sql"));

        Assert.DoesNotContain("MAX([CreatedDate])", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Administrator_grid_and_download_keep_every_row_of_the_month()
    {
        var source = File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", "SalesTransactionRepository.cs"));

        foreach (var method in new[] { "SelectAllAsync", "SelectAllByPeriodAsync" })
        {
            var body = MethodBody(source, method);
            Assert.DoesNotContain("LatestCreatedDate", body, StringComparison.Ordinal);
            Assert.DoesNotContain("MAX(st.CreatedDate)", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Migration_installs_both_procedures_from_their_readable_source()
    {
        var migrationsFolder = FindFromSolutionRoot("GiselX.Web", "Context", "Migrations");
        var migration = Normalize(File.ReadAllText(Assert.Single(
            Directory.GetFiles(migrationsFolder, "*_SelloutDateRangeReplacement.cs"))));

        foreach (var procedure in new[] { "GiselSales_Select", "GiselSales_SelectByCustPeriod" })
        {
            var source = Normalize(File.ReadAllText(
                FindFromSolutionRoot("GiselX.Web", "StoredProcedures", $"{procedure}.sql")));
            Assert.Contains(source, migration, StringComparison.Ordinal);
        }
    }

    private static string MethodBody(string source, string method)
    {
        // The declaration, not a call site: a line that starts a member and names the method.
        var declaration = Regex.Match(source, $@"\n    (?:private|internal|public)[^\n]*\b{method}\(");
        Assert.True(declaration.Success, $"{method} was not found.");
        var next = Regex.Match(source[(declaration.Index + declaration.Length)..],
            @"\n    (?:private|internal|public)[^\n]*\(");
        return next.Success
            ? source.Substring(declaration.Index, declaration.Length + next.Index)
            : source[declaration.Index..];
    }

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
