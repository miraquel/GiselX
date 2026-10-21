using System.Text.RegularExpressions;
using GiselX.Common.Constants;

namespace GiselX.Tests;

/// <summary>
/// The mapping and master-data screens were gated on Companies.Edit, which tied maintaining distributor
/// templates to the right to edit companies. They now declare their own permissions; these tests pin both
/// halves of that move — the constants themselves, and the backfill that keeps existing roles working.
/// </summary>
public sealed class MappingPermissionTests
{
    private static readonly string[] ManagedAreas =
    [
        "StockTemplateMappings", "DistributorTemplateMappings", "ItemMappings",
        "BusinessUnits", "Channels", "Salesmen"
    ];

    [Fact]
    public void Every_mapping_area_declares_its_own_index_manage_and_delete_permission()
    {
        var permissions = PermissionConstants.GetAllPermissions().ToHashSet(StringComparer.Ordinal);

        foreach (var area in ManagedAreas)
        {
            Assert.Contains($"{area}.Index", permissions);
            Assert.Contains($"{area}.Manage", permissions);
            Assert.Contains($"{area}.Delete", permissions);
        }

        // Bulk workbook import is riskier than editing one mapping, so it is delegated separately.
        Assert.Contains("ItemMappings.Import", permissions);
    }

    [Fact]
    public void Grouped_permissions_expose_each_mapping_area_as_its_own_group()
    {
        var grouped = PermissionConstants.GetGroupedPermissions();

        foreach (var area in ManagedAreas)
        {
            Assert.True(grouped.ContainsKey(area), $"Expected a '{area}' permission group.");
            Assert.NotEmpty(grouped[area]);
        }
    }

    [Fact]
    public void Backfill_script_grants_every_new_permission_to_existing_company_editors()
    {
        var sql = ReadBackfillScript();

        foreach (var permission in ManagedAreas
                     .SelectMany(area => new[] { $"{area}.Index", $"{area}.Manage", $"{area}.Delete" })
                     .Append("ItemMappings.Import"))
        {
            Assert.Contains($"N'{permission}'", sql, StringComparison.Ordinal);
        }

        Assert.Contains("ClaimValue = N'Companies.Edit'", sql, StringComparison.Ordinal);
        // "identity" is a T-SQL keyword (the IDENTITY property), so the schema must be bracketed or the
        // batch fails to parse. EF brackets identifiers for us; hand-written SQL has to do it itself.
        Assert.Contains("[identity].[AspNetRoleClaims]", sql, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?<!\[)\bidentity\.AspNetRoleClaims", sql);
    }

    [Fact]
    public void Backfill_script_guards_reruns_and_records_history_inside_the_transaction()
    {
        var sql = ReadBackfillScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);

        // Re-running must add nothing a second time.
        Assert.Contains("WHERE NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);

        var historyInsert = sql.IndexOf("INSERT INTO dbo.__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase);
        var commit = sql.IndexOf("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase);
        Assert.True(historyInsert > 0 && historyInsert < commit,
            "Migration history must be recorded inside the transaction.");
    }

    [Fact]
    public void Backfill_script_never_removes_a_claim_or_touches_a_user()
    {
        // Matched as statements, not as bare words: the granted permission values themselves end in
        // ".Delete", so searching for the word alone would flag the grant list it is meant to protect.
        var compact = Regex.Replace(ReadBackfillScript(), @"\s+", " ");

        Assert.DoesNotMatch(@"(?i)DELETE\s+(FROM\s+)?[A-Za-z\[]", compact);
        Assert.DoesNotMatch(@"(?i)TRUNCATE\s+TABLE\b", compact);
        Assert.DoesNotMatch(@"(?i)DROP\s+(TABLE|INDEX|CONSTRAINT)\b", compact);
        Assert.DoesNotMatch(@"(?i)UPDATE\s+\[?identity\]?\.", compact);
        Assert.DoesNotContain("AspNetUsers", compact, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_brackets_the_reserved_identity_schema_just_as_the_script_does()
    {
        // The migration and the manual script carry the same statement, and the same trap: "identity" is
        // a T-SQL keyword, so an unbracketed schema name makes the whole batch fail to parse at startup.
        var migration = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "GiselX.Web", "Context",
            "Migrations", "20260911165830_GrantMappingPermissionsToCompanyEditors.cs"));

        Assert.Contains("[identity].[AspNetRoleClaims]", migration, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?<!\[)\bidentity\.AspNetRoleClaims", migration);
    }

    private static string ReadBackfillScript()
    {
        var scriptPath = Path.Combine(FindRepositoryRoot(), "GiselX.Web", "DatabaseScripts",
            "20260911165830_GrantMappingPermissionsToCompanyEditors.sql");

        Assert.True(File.Exists(scriptPath), $"Expected SQL deployment script at '{scriptPath}'.");
        return File.ReadAllText(scriptPath);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "GiselX.sln"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate GiselX.sln above '{AppContext.BaseDirectory}'.");
    }
}
