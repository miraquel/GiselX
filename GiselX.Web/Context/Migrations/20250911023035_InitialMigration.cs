using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Company",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServelDeliveryEx",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SoQty = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    SoQtyCalc = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    SoCreateDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ShippingDateRequest = table.Column<DateTime>(type: "datetime", nullable: true),
                    LeadTimeDelivery = table.Column<int>(type: "int", nullable: true),
                    ShippingDateRequestCalc = table.Column<DateTime>(type: "datetime", nullable: true),
                    ReceiptDateRequest = table.Column<DateTime>(type: "datetime", nullable: true),
                    LeadTimeReceipt = table.Column<int>(type: "int", nullable: true),
                    ReceiptDateRequestCalc = table.Column<DateTime>(type: "datetime", nullable: true),
                    DoQty = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    DoQtyCalc = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    DoDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    DoDateMin = table.Column<DateTime>(type: "datetime", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    PeriodDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Ontime = table.Column<int>(type: "int", nullable: true),
                    Infull = table.Column<int>(type: "int", nullable: true),
                    QuadranServel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServelDeliveryEx", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServelReceiptEx",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SoQty = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    SoQtyCalc = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    SoCreateDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ShippingDateRequest = table.Column<DateTime>(type: "datetime", nullable: true),
                    LeadTimeDelivery = table.Column<int>(type: "int", nullable: true),
                    ShippingDateRequestCalc = table.Column<DateTime>(type: "datetime", nullable: true),
                    ReceiptDateRequest = table.Column<DateTime>(type: "datetime", nullable: true),
                    LeadtimeReceipt = table.Column<int>(type: "int", nullable: true),
                    ReceiptDateRequestCalc = table.Column<DateTime>(type: "datetime", nullable: true),
                    DoQty = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    DoQtyCalc = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    DoDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    DoDateMin = table.Column<DateTime>(type: "datetime", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    PeriodDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Ontime = table.Column<int>(type: "int", nullable: true),
                    Infull = table.Column<int>(type: "int", nullable: true),
                    QuadranServel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServelReceiptEx", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransDist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SoCreateDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    LeadTimeDlv = table.Column<int>(type: "int", nullable: true),
                    LeadTimeRct = table.Column<int>(type: "int", nullable: true),
                    ItemId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    SoQty = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    KgPerUnit = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    DlvDateRequest = table.Column<DateTime>(type: "datetime", nullable: true),
                    RctDateRequest = table.Column<DateTime>(type: "datetime", nullable: true),
                    DoQty = table.Column<decimal>(type: "numeric(32,16)", nullable: true),
                    DoDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransDist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransDist_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "identity",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "identity",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "identity",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "identity",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "identity",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId",
                schema: "identity",
                table: "AspNetUsers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Company_Name",
                table: "Company",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransDist_CompanyId",
                table: "TransDist",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ServelDeliveryEx");

            migrationBuilder.DropTable(
                name: "ServelReceiptEx");

            migrationBuilder.DropTable(
                name: "TransDist");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Company");
        }
    }
}
