using GiselX.Web.Context.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GiselX.Tests;

public sealed class CompanyBusinessUnitMigrationTests
{
    [Fact]
    public void Master_upgrade_preserves_text_values_and_creates_the_company_relationship()
    {
        var migration = new InspectableMasterMigration();
        var operations = migration.GetUpOperations();

        Assert.Contains(operations.OfType<CreateTableOperation>(), operation => operation.Name == "BusinessUnit");
        Assert.Contains(operations.OfType<AddColumnOperation>(), operation =>
            operation is { Name: "BusinessUnitId", Table: "Company", IsNullable: true });
        Assert.Contains(operations.OfType<AddForeignKeyOperation>(), operation =>
            operation is { Name: "FK_Company_BusinessUnit_BusinessUnitId", PrincipalTable: "BusinessUnit" });
        var sql = string.Join('\n', operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        Assert.Contains("SELECT DISTINCT LTRIM(RTRIM([BusinessUnit]))", sql, StringComparison.Ordinal);
        Assert.Contains("BusinessUnitName", sql, StringComparison.Ordinal);
        Assert.Contains("Company_Insert", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Upgrade_adds_nullable_business_unit_and_updates_company_procedures()
    {
        var migration = new InspectableMigration();
        var operations = migration.GetUpOperations();
        var column = Assert.Single(operations.OfType<AddColumnOperation>(),
            operation => operation.Name == "BusinessUnit" && operation.Table == "Company");

        Assert.True(column.IsNullable);
        Assert.Equal(100, column.MaxLength);

        var sql = string.Join('\n', operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        foreach (var procedure in new[]
                 {
                     "Company_Insert", "Company_Update", "Company_SelectById", "Company_Select",
                     "Company_SelectForReminder"
                 })
            Assert.Contains($"[dbo].[{procedure}]", sql, StringComparison.Ordinal);
        Assert.Contains("@BusinessUnit", sql, StringComparison.Ordinal);
        Assert.Contains("[BusinessUnit] LIKE @BusinessUnit", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Rollback_restores_procedures_before_dropping_business_unit()
    {
        var migration = new InspectableMigration();
        var operations = migration.GetDownOperations();
        var dropIndex = operations.Select((operation, index) => (operation, index))
            .Single(value => value.operation is DropColumnOperation { Name: "BusinessUnit", Table: "Company" })
            .index;

        Assert.True(dropIndex > 0);
        Assert.All(operations.Take(dropIndex).OfType<SqlOperation>(), operation =>
            Assert.DoesNotContain("@BusinessUnit", operation.Sql, StringComparison.Ordinal));
    }

    private sealed class InspectableMigration : AddCompanyBusinessUnit
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

    private sealed class InspectableMasterMigration : CreateBusinessUnitMaster
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            base.Up(builder);
            return builder.Operations;
        }
    }
}
