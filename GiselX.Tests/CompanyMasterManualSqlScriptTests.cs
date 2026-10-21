using System.Text.RegularExpressions;

namespace GiselX.Tests;

public sealed class CompanyMasterManualSqlScriptTests
{
    private const string MigrationId = "20260904030000_CreateChannelAndSalesmanMaster";

    [Fact]
    public void Script_contains_transaction_and_rerun_guards()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMMIT TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROLLBACK TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.Company', N'U') IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.BusinessUnit', N'U') IS NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_creates_both_masters_behind_existence_checks()
    {
        var sql = ReadScript();

        foreach (var table in new[] { "Channel", "Salesman" })
        {
            Assert.Contains($"OBJECT_ID(N'dbo.{table}', N'U') IS NULL", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"CREATE TABLE dbo.{table}", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"CONSTRAINT PK_{table} PRIMARY KEY (Id)", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"UX_{table}_Code", sql, StringComparison.Ordinal);
        }

        Assert.Contains("Code nvarchar(50) NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Name nvarchar(200) NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CreatedBy nvarchar(256) NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_adds_the_nullable_company_columns_with_restricted_foreign_keys()
    {
        var sql = ReadScript();

        foreach (var column in new[] { "ChannelId", "SalesmanId" })
        {
            Assert.Contains($"COL_LENGTH(N'dbo.Company', N'{column}') IS NULL", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"ADD {column} int NULL", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"IX_Company_{column}", sql, StringComparison.Ordinal);
        }

        Assert.Contains("FK_Company_Channel_ChannelId", sql, StringComparison.Ordinal);
        Assert.Contains("FK_Company_Salesman_SalesmanId", sql, StringComparison.Ordinal);
        // No ON DELETE CASCADE anywhere: a master in use must block, never remove companies.
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_reinstalls_every_company_procedure_with_the_new_columns()
    {
        var sql = ReadScript();

        foreach (var procedure in new[]
                 {
                     "Company_Insert", "Company_Update", "Company_SelectById",
                     "Company_Select", "Company_SelectForReminder"
                 })
        {
            Assert.Contains($"CREATE OR ALTER PROCEDURE [dbo].[{procedure}]", sql, StringComparison.Ordinal);
        }

        Assert.Contains("channel.Name AS ChannelName", sql, StringComparison.Ordinal);
        Assert.Contains("salesman.Name AS SalesmanName", sql, StringComparison.Ordinal);
        Assert.Contains("@ChannelId INT = NULL", sql, StringComparison.Ordinal);
        Assert.Contains("@SalesmanId INT = NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_has_no_destructive_statement_against_existing_data()
    {
        var compact = Regex.Replace(ReadScript(), @"\s+", " ");

        Assert.DoesNotMatch(@"(?i)\bDROP\s+TABLE\b", compact);
        Assert.DoesNotMatch(@"(?i)\bDROP\s+COLUMN\b", compact);
        Assert.DoesNotMatch(@"(?i)\bTRUNCATE\s+TABLE\b", compact);
        Assert.DoesNotMatch(@"(?i)\bDELETE\s+(FROM\s+)?(?:dbo\.)?(Channel|Salesman|Company)\b", compact);
        Assert.DoesNotMatch(@"(?i)\bUPDATE\s+\[?dbo\]?\.?\[?Company\]?\s+SET\b(?![^;]*WHERE Id = @Id)", compact);
    }

    [Fact]
    public void Script_records_the_manually_applied_ef_migration_inside_the_transaction()
    {
        var sql = ReadScript();

        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(MigrationId, sql, StringComparison.Ordinal);
        Assert.Contains("10.0.8", sql, StringComparison.Ordinal);

        var historyInsert = sql.IndexOf("INSERT INTO dbo.__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase);
        var commit = sql.IndexOf("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase);
        Assert.InRange(historyInsert, 0, commit);
    }

    [Fact]
    public void Script_matches_the_ef_migration_it_mirrors()
    {
        var migration = File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "Context", "Migrations", $"{MigrationId}.cs"));

        Assert.Contains("UX_{table}_Code", migration, StringComparison.Ordinal);
        Assert.Contains("onDelete: ReferentialAction.Restrict", migration, StringComparison.Ordinal);
        Assert.Contains("maxLength: 50", migration, StringComparison.Ordinal);
        Assert.Contains("maxLength: 200", migration, StringComparison.Ordinal);
        Assert.Contains("channel.Name AS ChannelName", migration, StringComparison.Ordinal);
        Assert.Contains("salesman.Name AS SalesmanName", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_down_restores_the_company_procedures_without_the_new_columns()
    {
        var migration = File.ReadAllText(FindFromSolutionRoot(
            "GiselX.Web", "Context", "Migrations", $"{MigrationId}.cs"));
        var down = migration[migration.IndexOf("protected override void Down", StringComparison.Ordinal)..];

        Assert.Contains("PreviousCompanyInsert", down, StringComparison.Ordinal);
        Assert.Contains("PreviousCompanySelect", down, StringComparison.Ordinal);
        Assert.DoesNotContain("@ChannelId", PreviousProcedure(migration, "PreviousCompanyInsert"), StringComparison.Ordinal);
        Assert.DoesNotContain("@SalesmanId", PreviousProcedure(migration, "PreviousCompanyUpdate"), StringComparison.Ordinal);
    }

    private static string PreviousProcedure(string source, string name)
    {
        var start = source.IndexOf($"private const string {name} = \"\"\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {name}.");
        var end = source.IndexOf("\"\"\";", start, StringComparison.Ordinal);
        return source[start..end];
    }

    private static string ReadScript() => File.ReadAllText(FindFromSolutionRoot(
        "GiselX.Web", "DatabaseScripts", $"{MigrationId}.sql"));

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
