using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260810120000_AddMultiWeekSelloutUploadGroup")]
public partial class AddMultiWeekSelloutUploadGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SelloutUploadGroup",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CompanyId = table.Column<int>(type: "int", nullable: false),
                DistributorTemplateMappingId = table.Column<int>(type: "int", nullable: true),
                OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                FileSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                TotalRows = table.Column<int>(type: "int", nullable: false),
                ValidRows = table.Column<int>(type: "int", nullable: false),
                InvalidRows = table.Column<int>(type: "int", nullable: false),
                IgnoredSummaryRowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ValidationErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UploadedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SelloutUploadGroup", x => x.Id);
                table.ForeignKey(
                    name: "FK_SelloutUploadGroup_Company_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Company",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SelloutUploadGroup_DistributorTemplateMapping_DistributorTemplateMappingId",
                    column: x => x.DistributorTemplateMappingId,
                    principalTable: "DistributorTemplateMapping",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<long>(
            name: "SelloutUploadGroupId",
            table: "SelloutUploadBatch",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SelloutUploadBatch_SelloutUploadGroupId",
            table: "SelloutUploadBatch",
            column: "SelloutUploadGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_SelloutUploadGroup_CompanyId_UploadedDate",
            table: "SelloutUploadGroup",
            columns: new[] { "CompanyId", "UploadedDate" });

        migrationBuilder.CreateIndex(
            name: "IX_SelloutUploadGroup_DistributorTemplateMappingId",
            table: "SelloutUploadGroup",
            column: "DistributorTemplateMappingId");

        migrationBuilder.AddForeignKey(
            name: "FK_SelloutUploadBatch_SelloutUploadGroup_SelloutUploadGroupId",
            table: "SelloutUploadBatch",
            column: "SelloutUploadGroupId",
            principalTable: "SelloutUploadGroup",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SelloutUploadBatch_SelloutUploadGroup_SelloutUploadGroupId",
            table: "SelloutUploadBatch");

        migrationBuilder.DropIndex(
            name: "IX_SelloutUploadBatch_SelloutUploadGroupId",
            table: "SelloutUploadBatch");

        migrationBuilder.DropColumn(
            name: "SelloutUploadGroupId",
            table: "SelloutUploadBatch");

        migrationBuilder.DropTable(name: "SelloutUploadGroup");
    }
}
