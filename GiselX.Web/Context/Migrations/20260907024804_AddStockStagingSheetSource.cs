using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddStockStagingSheetSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceWorksheetName",
                table: "StockStaging",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockTemplateMappingId",
                table: "StockStaging",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockStaging_StockTemplateMappingId",
                table: "StockStaging",
                column: "StockTemplateMappingId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockStaging_StockTemplateMapping_StockTemplateMappingId",
                table: "StockStaging",
                column: "StockTemplateMappingId",
                principalTable: "StockTemplateMapping",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockStaging_StockTemplateMapping_StockTemplateMappingId",
                table: "StockStaging");

            migrationBuilder.DropIndex(
                name: "IX_StockStaging_StockTemplateMappingId",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "SourceWorksheetName",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "StockTemplateMappingId",
                table: "StockStaging");
        }
    }
}
