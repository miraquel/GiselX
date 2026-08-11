using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260810190000_AddStockPriceUnit")]
public partial class AddStockPriceUnit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.Stock', N'PriceUnit') IS NULL
                ALTER TABLE dbo.Stock ADD PriceUnit decimal(18,2) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
                  AND c.name = N'PriceUnit'
                  AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                       OR c.scale <> 2 OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.Stock ALTER COLUMN PriceUnit decimal(18,2) NULL;

            IF COL_LENGTH(N'dbo.StockStaging', N'PriceUnit') IS NULL
                ALTER TABLE dbo.StockStaging ADD PriceUnit decimal(18,2) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
                  AND c.name = N'PriceUnit'
                  AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                       OR c.scale <> 2 OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.StockStaging ALTER COLUMN PriceUnit decimal(18,2) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Columns are retained because either column may predate this migration.
    }
}
