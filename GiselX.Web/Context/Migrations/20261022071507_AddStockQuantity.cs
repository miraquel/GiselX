using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddStockQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QtyCarton",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QtyPcs",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QtyCarton",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QtyPcs",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QtyCarton",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "QtyPcs",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "QtyCarton",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "QtyPcs",
                table: "Stock");
        }
    }
}
