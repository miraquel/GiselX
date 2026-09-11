using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260824160000_AllowStockSourceColumnReuse")]
public partial class AllowStockSourceColumnReuse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber",
            table: "StockTemplateColumn");

        migrationBuilder.CreateIndex(
            name: "IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber",
            table: "StockTemplateColumn",
            columns: new[] { "StockTemplateMappingId", "SourceColumnNumber" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber",
            table: "StockTemplateColumn");

        migrationBuilder.CreateIndex(
            name: "IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber",
            table: "StockTemplateColumn",
            columns: new[] { "StockTemplateMappingId", "SourceColumnNumber" },
            unique: true);
    }
}
