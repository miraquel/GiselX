using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyServelDeliveryExRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ServelDeliveryEx",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServelDeliveryEx_CompanyId",
                table: "ServelDeliveryEx",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServelDeliveryEx_Company_CompanyId",
                table: "ServelDeliveryEx",
                column: "CompanyId",
                principalTable: "Company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServelDeliveryEx_Company_CompanyId",
                table: "ServelDeliveryEx");

            migrationBuilder.DropIndex(
                name: "IX_ServelDeliveryEx_CompanyId",
                table: "ServelDeliveryEx");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ServelDeliveryEx");
        }
    }
}
