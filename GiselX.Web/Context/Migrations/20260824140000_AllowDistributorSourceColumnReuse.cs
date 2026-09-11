using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260824140000_AllowDistributorSourceColumnReuse")]
public partial class AllowDistributorSourceColumnReuse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_DistributorTemplateColumn_DistributorTemplateMappingId_SourceColumnNumber",
            table: "DistributorTemplateColumn");

        migrationBuilder.CreateIndex(
            name: "IX_DistributorTemplateColumn_DistributorTemplateMappingId_SourceColumnNumber",
            table: "DistributorTemplateColumn",
            columns: new[] { "DistributorTemplateMappingId", "SourceColumnNumber" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_DistributorTemplateColumn_DistributorTemplateMappingId_SourceColumnNumber",
            table: "DistributorTemplateColumn");

        migrationBuilder.CreateIndex(
            name: "IX_DistributorTemplateColumn_DistributorTemplateMappingId_SourceColumnNumber",
            table: "DistributorTemplateColumn",
            columns: new[] { "DistributorTemplateMappingId", "SourceColumnNumber" },
            unique: true);
    }
}
