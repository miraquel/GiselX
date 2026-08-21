namespace GiselX.Tests;

public sealed class PowerBiReportManualSqlTests
{
    [Fact]
    public void Script_contains_transaction_rerun_and_schema_verification_guards()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(N'dbo.PowerBiReport'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dc.definition", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("normalised.Definition NOT IN", sql, StringComparison.Ordinal);
        Assert.Contains("N'convert[bit],1'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT LIKE N'%1%'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CK_PowerBiReport_DisplayOrder_NonNegative", sql, StringComparison.Ordinal);
        Assert.Contains("[DisplayOrder] >= 0", sql, StringComparison.Ordinal);
        Assert.Contains("c.is_identity <> 1", sql, StringComparison.Ordinal);
        Assert.Contains("sys.key_constraints", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PK_PowerBiReport", sql, StringComparison.Ordinal);
        Assert.Contains("i.is_primary_key = 1", sql, StringComparison.Ordinal);
        Assert.Contains("THROW", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_creates_the_report_table_indexes_and_restricted_company_foreign_key()
    {
        var sql = ReadScript();

        Assert.Contains("CREATE TABLE dbo.PowerBiReport", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UX_PowerBiReport_CompanyId_Name", sql, StringComparison.Ordinal);
        Assert.Contains("IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name", sql, StringComparison.Ordinal);
        Assert.Contains("REFERENCES dbo.Company(Id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_records_the_ef_migration_inside_its_transaction()
    {
        var sql = ReadScript();

        Assert.Contains("20260821095349_AddPowerBiReports", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10.0.8", sql, StringComparison.Ordinal);
        Assert.True(sql.IndexOf("INSERT INTO dbo.__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) <
                    sql.IndexOf("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadScript()
    {
        var path = Path.Combine(FindSolutionRoot(), "GiselX.Web", "DatabaseScripts", "20260821095349_AddPowerBiReports.sql");
        Assert.True(File.Exists(path), $"Expected SQL deployment script at '{path}'.");
        return File.ReadAllText(path);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate GiselX.sln.");
    }
}
