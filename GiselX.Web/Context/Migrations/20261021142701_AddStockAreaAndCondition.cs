using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAreaAndCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadConditionValues",
                table: "StockTemplateMapping",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoodConditionValues",
                table: "StockTemplateMapping",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Condition",
                table: "StockStaging",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "SourceAreaText",
                table: "StockStaging",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceConditionText",
                table: "StockStaging",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockAreaId",
                table: "StockStaging",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Condition",
                table: "Stock",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "StockAreaId",
                table: "Stock",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockArea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockArea_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockStaging_StockAreaId",
                table: "StockStaging",
                column: "StockAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_StockAreaId",
                table: "Stock",
                column: "StockAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_StockArea_CompanyId_Name",
                table: "StockArea",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Stock_StockArea_StockAreaId",
                table: "Stock",
                column: "StockAreaId",
                principalTable: "StockArea",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockStaging_StockArea_StockAreaId",
                table: "StockStaging",
                column: "StockAreaId",
                principalTable: "StockArea",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stock_StockArea_StockAreaId",
                table: "Stock");

            migrationBuilder.DropForeignKey(
                name: "FK_StockStaging_StockArea_StockAreaId",
                table: "StockStaging");

            migrationBuilder.DropTable(
                name: "StockArea");

            migrationBuilder.DropIndex(
                name: "IX_StockStaging_StockAreaId",
                table: "StockStaging");

            migrationBuilder.DropIndex(
                name: "IX_Stock_StockAreaId",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "BadConditionValues",
                table: "StockTemplateMapping");

            migrationBuilder.DropColumn(
                name: "GoodConditionValues",
                table: "StockTemplateMapping");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "SourceAreaText",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "SourceConditionText",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "StockAreaId",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "StockAreaId",
                table: "Stock");
        }
    }
}
