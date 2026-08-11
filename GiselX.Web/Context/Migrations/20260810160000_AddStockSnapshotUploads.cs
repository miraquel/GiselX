using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260810160000_AddStockSnapshotUploads")]
public partial class AddStockSnapshotUploads : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "StockUploadBatchId", table: "Stock", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "StockDate", table: "Stock", type: "date", nullable: true);

        migrationBuilder.CreateTable(
            name: "StockTemplateMapping",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
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
                table.PrimaryKey("PK_StockTemplateMapping", x => x.Id);
                table.CheckConstraint("CK_StockTemplateMapping_FirstDataRow", "[FirstDataRowNumber] > [HeaderRowNumber]");
                table.CheckConstraint("CK_StockTemplateMapping_HeaderRow", "[HeaderRowNumber] > 0");
                table.ForeignKey("FK_StockTemplateMapping_Company_CompanyId", x => x.CompanyId,
                    "Company", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StockTemplateColumn",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                StockTemplateMappingId = table.Column<int>(type: "int", nullable: false),
                SourceColumnNumber = table.Column<int>(type: "int", nullable: false),
                SourceHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CanonicalField = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockTemplateColumn", x => x.Id);
                table.ForeignKey("FK_StockTemplateColumn_StockTemplateMapping_StockTemplateMappingId",
                    x => x.StockTemplateMappingId, "StockTemplateMapping", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StockUploadBatch",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                CompanyId = table.Column<int>(type: "int", nullable: false),
                StockTemplateMappingId = table.Column<int>(type: "int", nullable: true),
                StockDate = table.Column<DateTime>(type: "date", nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                FileSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                TotalRows = table.Column<int>(type: "int", nullable: false),
                ValidRows = table.Column<int>(type: "int", nullable: false),
                InvalidRows = table.Column<int>(type: "int", nullable: false),
                IgnoredSummaryRowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                ValidationErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UploadedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockUploadBatch", x => x.Id);
                table.ForeignKey("FK_StockUploadBatch_Company_CompanyId", x => x.CompanyId,
                    "Company", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_StockUploadBatch_StockTemplateMapping_StockTemplateMappingId",
                    x => x.StockTemplateMappingId, "StockTemplateMapping", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StockStaging",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                StockUploadBatchId = table.Column<long>(type: "bigint", nullable: false),
                SourceRowNumber = table.Column<int>(type: "int", nullable: false),
                ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ProductPackaging = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProductPcsInCtn = table.Column<int>(type: "int", nullable: true),
                ProductNetto = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                ProductUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SaldoAwal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                SaldoMasukPO = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                SaldoAkhir = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExpiredDate = table.Column<DateTime>(type: "datetime", nullable: true),
                IsValid = table.Column<bool>(type: "bit", nullable: false),
                ValidationErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockStaging", x => x.Id);
                table.ForeignKey("FK_StockStaging_StockUploadBatch_StockUploadBatchId", x => x.StockUploadBatchId,
                    "StockUploadBatch", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Stock_StockUploadBatchId", "Stock", "StockUploadBatchId");
        migrationBuilder.CreateIndex("IX_StockTemplateMapping_CompanyId_Name", "StockTemplateMapping",
            new[] { "CompanyId", "Name" }, unique: true);
        migrationBuilder.CreateIndex("IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber",
            "StockTemplateColumn", new[] { "StockTemplateMappingId", "SourceColumnNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_StockTemplateColumn_StockTemplateMappingId_CanonicalField",
            "StockTemplateColumn", new[] { "StockTemplateMappingId", "CanonicalField" }, unique: true);
        migrationBuilder.CreateIndex("IX_StockUploadBatch_CompanyId_CompletedDate_Id", "StockUploadBatch",
            new[] { "CompanyId", "CompletedDate", "Id" });
        migrationBuilder.CreateIndex("IX_StockUploadBatch_CompanyId_StockDate", "StockUploadBatch",
            new[] { "CompanyId", "StockDate" });
        migrationBuilder.CreateIndex("IX_StockUploadBatch_Status", "StockUploadBatch", "Status");
        migrationBuilder.CreateIndex("IX_StockUploadBatch_StockTemplateMappingId", "StockUploadBatch", "StockTemplateMappingId");
        migrationBuilder.CreateIndex("IX_StockStaging_StockUploadBatchId_SourceRowNumber", "StockStaging",
            new[] { "StockUploadBatchId", "SourceRowNumber" });

        migrationBuilder.AddForeignKey("FK_Stock_StockUploadBatch_StockUploadBatchId", "Stock", "StockUploadBatchId",
            "StockUploadBatch", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Stock_StockUploadBatch_StockUploadBatchId", "Stock");
        migrationBuilder.DropTable("StockStaging");
        migrationBuilder.DropTable("StockTemplateColumn");
        migrationBuilder.DropTable("StockUploadBatch");
        migrationBuilder.DropTable("StockTemplateMapping");
        migrationBuilder.DropIndex("IX_Stock_StockUploadBatchId", "Stock");
        migrationBuilder.DropColumn("StockUploadBatchId", "Stock");
        migrationBuilder.DropColumn("StockDate", "Stock");
    }
}
