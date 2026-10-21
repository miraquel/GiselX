using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260915090000_AddBusinessUnitNotificationEmails")]
public partial class AddBusinessUnitNotificationEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.BusinessUnit', N'NotificationEmails') IS NULL
                ALTER TABLE dbo.BusinessUnit ADD NotificationEmails nvarchar(1000) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.BusinessUnit', N'U')
                  AND c.name = N'NotificationEmails'
                  AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000
                       OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.BusinessUnit ALTER COLUMN NotificationEmails nvarchar(1000) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The column is retained: dropping it would discard the recipient lists an operator typed in,
        // and it is nullable, so leaving it costs nothing.
    }
}
