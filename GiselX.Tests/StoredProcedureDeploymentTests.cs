using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// A stored procedure reaches a database only because a migration's Sql(...) creates it — nothing
/// deploys StoredProcedures/*.sql. GiselSales_SelectPeriods was called by the repository for months
/// while existing only in databases where someone had run that file by hand, and a database built
/// from migrations failed the Sales Transactions filters. No test
/// executes SQL, so this pins the one property that would have caught it.
/// </summary>
public sealed class StoredProcedureDeploymentTests
{
    /// <summary>
    /// Called by a repository yet deliberately not created by any migration.
    /// </summary>
    private static readonly HashSet<string> NotDeployedByMigrations = new(StringComparer.Ordinal)
    {
        // The legacy Service Level procedures predate the EF migrations and already exist on every
        // database the application has run against.
        "Gisel_Select", "Gisel_SelectByCust", "Gisel_SelectByCustPeriod", "Gisel_SelectPeriods", "Gisel_del",

        // Repository methods that no service calls. Deploying them would publish unused objects; if
        // one of these methods is wired up again, remove it from this list and add a migration.
        "GiselSales_SelectByCust", "GiselSales_del", "GiselStock_SelectByCust", "GiselStock_SelectByCustPeriod",
    };

    [Fact]
    public void Every_procedure_a_repository_calls_is_created_by_a_migration()
    {
        var called = Directory.GetFiles(FindFromSolutionRoot("GiselX.Repository"), "*.cs")
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "\"((?:Company|Gisel[A-Za-z]*)_[A-Za-z]+)\"")
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var migrations = string.Join("\n", Directory.GetFiles(
                FindFromSolutionRoot("GiselX.Web", "Context", "Migrations"), "*.cs")
            .Select(File.ReadAllText));

        var missing = called
            .Where(name => !NotDeployedByMigrations.Contains(name))
            .Where(name => !Regex.IsMatch(migrations,
                $@"CREATE\s+OR\s+ALTER\s+PROCEDURE\s+\[dbo\]\.\[{name}\]", RegexOptions.IgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Called by a repository but created by no migration: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void Select_periods_migration_and_script_install_the_readable_source()
    {
        const string procedure = "GiselSales_SelectPeriods";
        var source = Normalize(File.ReadAllText(
            FindFromSolutionRoot("GiselX.Web", "StoredProcedures", $"{procedure}.sql")));
        var migration = Normalize(File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "Context", "Migrations", "20260929090000_AddSelectPeriodsProcedures.cs")));
        var script = Normalize(File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "DatabaseScripts", "20260929090000_AddSelectPeriodsProcedures.sql")));

        Assert.Contains(source, migration, StringComparison.Ordinal);
        Assert.Contains(source, script, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_periods_script_is_transactional_rerunnable_and_records_history()
    {
        var sql = File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "DatabaseScripts", "20260929090000_AddSelectPeriodsProcedures.sql"));

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'20260929090000_AddSelectPeriodsProcedures'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP PROCEDURE", sql, StringComparison.OrdinalIgnoreCase);
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
