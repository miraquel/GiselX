using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

/// <summary>
/// Internal notification recipients are no longer held only on the Business Unit. A distributor also
/// has a Channel and a Salesman, and the GMK people who want to know an upload landed are often
/// attached to those rather than to the unit — the salesman accountable for the distributor most of
/// all. Both masters gain the same delimited list the Business Unit already has.
/// </summary>
[DbContext(typeof(GiselXDbContext))]
[Migration("20260916120000_AddChannelAndSalesmanNotificationEmails")]
public partial class AddChannelAndSalesmanNotificationEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "Channel", "Salesman" })
        {
            migrationBuilder.Sql($"""
                IF COL_LENGTH(N'dbo.{table}', N'NotificationEmails') IS NULL
                    ALTER TABLE dbo.{table} ADD NotificationEmails nvarchar(1000) NULL;
                ELSE IF EXISTS
                (
                    SELECT 1 FROM sys.columns c
                    WHERE c.object_id = OBJECT_ID(N'dbo.{table}', N'U')
                      AND c.name = N'NotificationEmails'
                      AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000
                           OR c.is_nullable <> 1)
                )
                    ALTER TABLE dbo.{table} ALTER COLUMN NotificationEmails nvarchar(1000) NULL;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The columns are retained: dropping them would discard recipient lists an operator typed,
        // and they are nullable, so leaving them costs nothing.
    }
}
