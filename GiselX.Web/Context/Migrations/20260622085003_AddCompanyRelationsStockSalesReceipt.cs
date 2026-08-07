using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyRelationsStockSalesReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ServelReceiptEx",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SalesTransaction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerAlias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductBrand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductPackaging = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductPcsInCtn = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiredDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    InvoiceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    InvoiceQty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPct = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InKg = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GeisaPOId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShipDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    SalesmanNameGMK = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesTransaction_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductPackaging = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductPcsInCtn = table.Column<int>(type: "int", nullable: false),
                    ProductNetto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SaldoAwal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoMasukPO = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoAkhir = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiredDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stock_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServelReceiptEx_CompanyId",
                table: "ServelReceiptEx",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransaction_CompanyId_CreatedDate",
                table: "SalesTransaction",
                columns: new[] { "CompanyId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Stock_CompanyId_CreatedDate",
                table: "Stock",
                columns: new[] { "CompanyId", "CreatedDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServelReceiptEx_Company_CompanyId",
                table: "ServelReceiptEx",
                column: "CompanyId",
                principalTable: "Company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServelReceiptEx_Company_CompanyId",
                table: "ServelReceiptEx");

            migrationBuilder.DropTable(
                name: "SalesTransaction");

            migrationBuilder.DropTable(
                name: "Stock");

            migrationBuilder.DropIndex(
                name: "IX_ServelReceiptEx_CompanyId",
                table: "ServelReceiptEx");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ServelReceiptEx");
        }
    }
}
