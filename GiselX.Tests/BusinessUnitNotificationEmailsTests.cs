using System.Text.RegularExpressions;
using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public sealed class BusinessUnitNotificationEmailsPersistenceModelTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXBusinessUnitModelTest;Trusted_Connection=True")
        .Options);

    [Fact]
    public void Model_maps_notification_emails_as_an_optional_bounded_column()
    {
        using var db = CreateContext();
        var property = db.Model.FindEntityType(typeof(BusinessUnit))!
            .FindProperty(nameof(BusinessUnit.NotificationEmails))!;

        // Nullable on purpose: a unit that notifies nobody internally must stay NULL rather than
        // carry an empty string the mailer would then have to special-case.
        Assert.True(property.IsNullable);
        Assert.Equal(1000, property.GetMaxLength());
    }
}

public sealed class BusinessUnitNotificationEmailsManualSqlScriptTests
{
    private const string MigrationId = "20260915090000_AddBusinessUnitNotificationEmails";

    [Fact]
    public void Script_contains_transaction_and_rerun_guards()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.BusinessUnit'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COL_LENGTH(N'dbo.BusinessUnit', N'NotificationEmails')", sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_adds_the_approved_column_shape()
    {
        var sql = ReadScript();

        Assert.Contains("ADD NotificationEmails nvarchar(1000) NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_has_no_destructive_business_unit_statement()
    {
        var compact = Regex.Replace(ReadScript(), @"\s+", " ");

        Assert.DoesNotMatch(@"(?i)DELETE\s+(FROM\s+)?(?:dbo\.)?BusinessUnit\b", compact);
        Assert.DoesNotMatch(@"(?i)UPDATE\s+(?:dbo\.)?BusinessUnit\s+SET\b", compact);
        Assert.DoesNotMatch(@"(?i)DROP\s+COLUMN\s+NotificationEmails\b", compact);
        Assert.DoesNotMatch(@"(?i)TRUNCATE\s+TABLE\s+(?:dbo\.)?BusinessUnit\b", compact);
    }

    [Fact]
    public void Script_records_the_manually_applied_ef_migration()
    {
        var sql = ReadScript();

        Assert.Contains("dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(MigrationId, sql, StringComparison.Ordinal);
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
