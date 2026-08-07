using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Company",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeadlineDayOfMonth",
                table: "Company",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeadlineDaysOfWeek",
                table: "Company",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderLeadDays",
                table: "Company",
                type: "int",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "DeadlineDayOfMonth",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "DeadlineDaysOfWeek",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "ReminderLeadDays",
                table: "Company");
        }
    }
}
