using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributorSelloutMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportingWeekStart",
                table: "SalesTransaction",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SelloutUploadBatchId",
                table: "SalesTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DistributorTemplateMapping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorksheetName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HeaderRowNumber = table.Column<int>(type: "int", nullable: false),
                    FirstDataRowNumber = table.Column<int>(type: "int", nullable: false),
                    HeaderSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributorTemplateMapping", x => x.Id);
                    table.CheckConstraint("CK_DistributorTemplateMapping_FirstDataRow", "[FirstDataRowNumber] > [HeaderRowNumber]");
                    table.CheckConstraint("CK_DistributorTemplateMapping_HeaderRow", "[HeaderRowNumber] > 0");
                    table.ForeignKey(
                        name: "FK_DistributorTemplateMapping_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributorTemplateColumn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistributorTemplateMappingId = table.Column<int>(type: "int", nullable: false),
                    SourceColumnNumber = table.Column<int>(type: "int", nullable: false),
                    SourceHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CanonicalField = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributorTemplateColumn", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributorTemplateColumn_DistributorTemplateMapping_DistributorTemplateMappingId",
                        column: x => x.DistributorTemplateMappingId,
                        principalTable: "DistributorTemplateMapping",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SelloutUploadBatch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DistributorTemplateMappingId = table.Column<int>(type: "int", nullable: true),
                    ReportingWeekStart = table.Column<DateTime>(type: "date", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    InvalidRows = table.Column<int>(type: "int", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelloutUploadBatch", x => x.Id);
                    table.CheckConstraint("CK_SelloutUploadBatch_ReportingWeekMonday", "DATEDIFF(day, '19000101', [ReportingWeekStart]) % 7 = 0");
                    table.ForeignKey(
                        name: "FK_SelloutUploadBatch_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SelloutUploadBatch_DistributorTemplateMapping_DistributorTemplateMappingId",
                        column: x => x.DistributorTemplateMappingId,
                        principalTable: "DistributorTemplateMapping",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SelloutStaging",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SelloutUploadBatchId = table.Column<long>(type: "bigint", nullable: false),
                    SourceRowNumber = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerAlias = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductBrand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductPackaging = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductPcsInCtn = table.Column<int>(type: "int", nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    InvoiceNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    InvoiceQty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InvoiceUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GrossValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountPct = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InKg = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GeisaPOId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShipDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    SalesmanNameGMK = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    ValidationErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelloutStaging", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelloutStaging_SelloutUploadBatch_SelloutUploadBatchId",
                        column: x => x.SelloutUploadBatchId,
                        principalTable: "SelloutUploadBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransaction_CompanyId_ReportingWeekStart",
                table: "SalesTransaction",
                columns: new[] { "CompanyId", "ReportingWeekStart" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransaction_SelloutUploadBatchId",
                table: "SalesTransaction",
                column: "SelloutUploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributorTemplateColumn_DistributorTemplateMappingId_CanonicalField",
                table: "DistributorTemplateColumn",
                columns: new[] { "DistributorTemplateMappingId", "CanonicalField" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributorTemplateColumn_DistributorTemplateMappingId_SourceColumnNumber",
                table: "DistributorTemplateColumn",
                columns: new[] { "DistributorTemplateMappingId", "SourceColumnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributorTemplateMapping_CompanyId_Name",
                table: "DistributorTemplateMapping",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelloutStaging_SelloutUploadBatchId_SourceRowNumber",
                table: "SelloutStaging",
                columns: new[] { "SelloutUploadBatchId", "SourceRowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SelloutUploadBatch_CompanyId_ReportingWeekStart",
                table: "SelloutUploadBatch",
                columns: new[] { "CompanyId", "ReportingWeekStart" },
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_SelloutUploadBatch_DistributorTemplateMappingId",
                table: "SelloutUploadBatch",
                column: "DistributorTemplateMappingId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesTransaction_SelloutUploadBatch_SelloutUploadBatchId",
                table: "SalesTransaction",
                column: "SelloutUploadBatchId",
                principalTable: "SelloutUploadBatch",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesTransaction_SelloutUploadBatch_SelloutUploadBatchId",
                table: "SalesTransaction");

            migrationBuilder.DropTable(
                name: "DistributorTemplateColumn");

            migrationBuilder.DropTable(
                name: "SelloutStaging");

            migrationBuilder.DropTable(
                name: "SelloutUploadBatch");

            migrationBuilder.DropTable(
                name: "DistributorTemplateMapping");

            migrationBuilder.DropIndex(
                name: "IX_SalesTransaction_CompanyId_ReportingWeekStart",
                table: "SalesTransaction");

            migrationBuilder.DropIndex(
                name: "IX_SalesTransaction_SelloutUploadBatchId",
                table: "SalesTransaction");

            migrationBuilder.DropColumn(
                name: "ReportingWeekStart",
                table: "SalesTransaction");

            migrationBuilder.DropColumn(
                name: "SelloutUploadBatchId",
                table: "SalesTransaction");
        }
    }
}
