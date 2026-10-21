using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

/// <summary>
/// SMTP configuration moves into the database. It lived in appsettings.json, which a copy-paste
/// deployment overwrites — the same way the dev server lost its connection string and failed to
/// start — so the one setting an operator must be able to fix without a deploy now survives one.
/// </summary>
[DbContext(typeof(GiselXDbContext))]
[Migration("20260916090000_AddEmailSetting")]
public partial class AddEmailSetting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.EmailSetting', N'U') IS NULL
            CREATE TABLE dbo.EmailSetting
            (
                Id                int            NOT NULL CONSTRAINT PK_EmailSetting PRIMARY KEY,
                SmtpHost          nvarchar(255)  NOT NULL,
                SmtpPort          int            NOT NULL,
                EnableSsl         bit            NOT NULL,
                Username          nvarchar(255)  NULL,
                PasswordProtected nvarchar(2000) NULL,
                FromAddress       nvarchar(255)  NOT NULL,
                FromName          nvarchar(255)  NOT NULL,
                DashboardBaseUrl  nvarchar(500)  NULL,
                SupportEmail      nvarchar(255)  NULL,
                SupportPhone      nvarchar(100)  NULL,
                TimeZoneId        nvarchar(100)  NOT NULL,
                ModifiedDate      datetime2      NULL,
                ModifiedBy        nvarchar(256)  NULL,
                CONSTRAINT CK_EmailSetting_Singleton CHECK (Id = 1)
            );
            """);

        // Every role that may already fire a test message gains the right to configure SMTP, so the
        // screen is reachable after deployment without anyone editing role claims by hand. The
        // schema is bracketed because "identity" is the T-SQL IDENTITY keyword and will not parse
        // bare — the mistake that took startup down once already.
        migrationBuilder.Sql("""
            INSERT INTO [identity].[AspNetRoleClaims] (RoleId, ClaimType, ClaimValue)
            SELECT existing.RoleId, N'permission', N'EmailSettings.Manage'
            FROM [identity].[AspNetRoleClaims] existing
            WHERE existing.ClaimType = N'permission'
              AND existing.ClaimValue = N'EmailTest.Send'
              AND NOT EXISTS
              (
                  SELECT 1 FROM [identity].[AspNetRoleClaims] already
                  WHERE already.RoleId = existing.RoleId
                    AND already.ClaimType = N'permission'
                    AND already.ClaimValue = N'EmailSettings.Manage'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retained deliberately: dropping it would discard the SMTP configuration an operator
        // entered, and rolling the code back does not make that configuration wrong.
    }
}
