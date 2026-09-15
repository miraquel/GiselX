using System.Text.RegularExpressions;
using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public sealed class CompanyContactEmailPersistenceModelTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXCompanyContactEmailTest;Trusted_Connection=True")
        .Options);

    [Fact]
    public void Model_widens_contact_email_to_hold_a_delimited_list()
    {
        using var db = CreateContext();
        var property = db.Model.FindEntityType(typeof(Company))!
            .FindProperty(nameof(Company.ContactEmail))!;

        // 255 held one address. The field is now a ';'-delimited list feeding both the reminder and
        // the upload confirmation, so it matches BusinessUnit.NotificationEmails.
        Assert.Equal(1000, property.GetMaxLength());
        Assert.True(property.IsNullable);
    }
}

public sealed class CompanyContactEmailManualSqlScriptTests
{
    private const string MigrationId = "20260915120000_WidenCompanyContactEmail";

    [Fact]
    public void Script_contains_transaction_and_rerun_guards()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.Company'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_widens_the_column_to_the_approved_shape()
    {
        var sql = ReadScript();

        Assert.Contains("ALTER COLUMN ContactEmail nvarchar(1000) NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_reinstalls_every_procedure_that_carries_the_value()
    {
        // The whole point of the change: widening the column without these leaves @ContactEmail at
        // NVARCHAR(255) and truncates longer lists silently on the way in.
        var sql = ReadScript();

        foreach (var procedure in new[] { "Company_Insert", "Company_Update", "Company_Select" })
        {
            Assert.Contains($"[dbo].[{procedure}]", sql, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("@ContactEmail       NVARCHAR(255)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@ContactEmail   NVARCHAR(255)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_has_no_destructive_company_statement()
    {
        var compact = Regex.Replace(ReadScript(), @"\s+", " ");

        Assert.DoesNotMatch(@"(?i)DELETE\s+(FROM\s+)?(?:dbo\.)?Company\b", compact);
        Assert.DoesNotMatch(@"(?i)TRUNCATE\s+TABLE\s+(?:dbo\.)?Company\b", compact);
        Assert.DoesNotMatch(@"(?i)DROP\s+COLUMN\s+ContactEmail\b", compact);
    }

    [Fact]
    public void Script_records_the_manually_applied_ef_migration()
    {
        var sql = ReadScript();

        Assert.Contains("dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(MigrationId, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Stored_procedure_sources_declare_the_widened_parameter()
    {
        // The .sql files are the readable source of each procedure and the migration is generated
        // from them; if they drift back to 255 the deployed procedure would too.
        foreach (var procedure in new[] { "Company_Insert", "Company_Update", "Company_Select" })
        {
            var sql = System.IO.File.ReadAllText(System.IO.Path.Combine(
                RepositoryRoot(), "GiselX.Web", "StoredProcedures", $"{procedure}.sql"));

            Assert.Matches(@"@ContactEmail\s+NVARCHAR\(1000\)", sql);
        }
    }

    private static string ReadScript() =>
        System.IO.File.ReadAllText(System.IO.Path.Combine(
            RepositoryRoot(), "GiselX.Web", "DatabaseScripts", $"{MigrationId}.sql"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("GiselX.sln was not found.");
    }
}
