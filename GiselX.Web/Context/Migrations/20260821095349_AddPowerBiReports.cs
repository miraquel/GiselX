using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerBiReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PowerBiReport",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmbedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerBiReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PowerBiReport_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name",
                table: "PowerBiReport",
                columns: new[] { "CompanyId", "IsActive", "DisplayOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_PowerBiReport_CompanyId_Name",
                table: "PowerBiReport",
                columns: new[] { "CompanyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerBiReport");
        }
    }
}
