using GiselX.Web.Context.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GiselX.Tests;

public sealed class PowerBiReportMigrationTests
{
    [Fact]
    public void Migration_creates_the_report_table_with_the_required_indexes_and_restricted_company_foreign_key()
    {
        var migrationSource = ReadMigrationSource();

        Assert.Contains("CreateTable", migrationSource, StringComparison.Ordinal);
        Assert.Contains("PowerBiReport", migrationSource, StringComparison.Ordinal);
        Assert.Contains("UX_PowerBiReport_CompanyId_Name", migrationSource, StringComparison.Ordinal);
        Assert.Contains("IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name", migrationSource, StringComparison.Ordinal);
        Assert.Contains("onDelete: ReferentialAction.Restrict", migrationSource, StringComparison.Ordinal);
        Assert.Contains("defaultValue: true", migrationSource, StringComparison.Ordinal);
        Assert.Contains("CK_PowerBiReport_DisplayOrder_NonNegative", migrationSource, StringComparison.Ordinal);
        Assert.Contains("[DisplayOrder] >= 0", migrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_operations_create_the_complete_contract_and_down_only_drops_the_new_table()
    {
        var migration = new InspectablePowerBiReportMigration();
        var upOperations = migration.GetUpOperations();
        var createTable = Assert.Single(upOperations.OfType<CreateTableOperation>(), operation => operation.Name == "PowerBiReport");

        Assert.Contains(createTable.Columns, column => column.Name == "IsActive" && Equals(column.DefaultValue, true));
        Assert.Contains(createTable.CheckConstraints, constraint =>
            constraint.Name == "CK_PowerBiReport_DisplayOrder_NonNegative" &&
            constraint.Sql == "[DisplayOrder] >= 0");
        Assert.Single(createTable.ForeignKeys, key =>
            key.PrincipalTable == "Company" && key.OnDelete == ReferentialAction.Restrict);
        Assert.Contains(upOperations.OfType<CreateIndexOperation>(), operation =>
            operation.Name == "UX_PowerBiReport_CompanyId_Name" && operation.IsUnique);
        Assert.Contains(upOperations.OfType<CreateIndexOperation>(), operation =>
            operation.Name == "IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name");

        var downOperations = migration.GetDownOperations();
        var drop = Assert.Single(downOperations.OfType<DropTableOperation>());
        Assert.Equal("PowerBiReport", drop.Name);
        Assert.Single(downOperations);
    }

    private static string ReadMigrationSource() => File.ReadAllText(Path.Combine(
        FindSolutionRoot(), "GiselX.Web", "Context", "Migrations", "20260821095349_AddPowerBiReports.cs"));

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate GiselX.sln.");
    }

    private sealed class InspectablePowerBiReportMigration : AddPowerBiReports
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> GetDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Down(builder);
            return builder.Operations;
        }
    }
}
