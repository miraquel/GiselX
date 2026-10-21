using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

/// <summary>
/// Installs GiselSales_SelectPeriods, which fills the Sales Transactions year and month pickers. The
/// repository has called it since the page was built, but no migration ever created it — it existed
/// only in databases where someone had run the StoredProcedures file by hand, so on a database built
/// purely from migrations the filters failed with "Could not find stored procedure". The body is
/// exactly StoredProcedures/GiselSales_SelectPeriods.sql. The Stock pickers read published snapshot
/// dates with inline SQL instead, so GiselStock_SelectPeriods is deliberately not installed.
/// </summary>
[DbContext(typeof(GiselXDbContext))]
[Migration("20260929090000_AddSelectPeriodsProcedures")]
public partial class AddSelectPeriodsProcedures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectPeriods]
                @CompanyId INT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET ARITHABORT ON;

                SELECT DISTINCT
                    YEAR([InvoiceDate])  AS [Year],
                    MONTH([InvoiceDate]) AS [Month]
                FROM [dbo].[SalesTransaction]
                WHERE [CompanyId] = @CompanyId
                ORDER BY [Year] DESC, [Month] DESC;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retained: databases that predate this migration may already have held this procedure,
        // installed by hand, and dropping it would leave such a database worse than it was.
    }
}
