using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260903120000_AddStockSmallestUnit")]
public partial class AddStockSmallestUnit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.Stock', N'SmallestUnit') IS NULL
                ALTER TABLE dbo.Stock ADD SmallestUnit nvarchar(50) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
                  AND c.name = N'SmallestUnit'
                  AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 100
                       OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.Stock ALTER COLUMN SmallestUnit nvarchar(50) NULL;

            IF COL_LENGTH(N'dbo.Stock', N'SmallestUnitQty') IS NULL
                ALTER TABLE dbo.Stock ADD SmallestUnitQty decimal(18,2) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
                  AND c.name = N'SmallestUnitQty'
                  AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                       OR c.scale <> 2 OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.Stock ALTER COLUMN SmallestUnitQty decimal(18,2) NULL;

            IF COL_LENGTH(N'dbo.StockStaging', N'SmallestUnit') IS NULL
                ALTER TABLE dbo.StockStaging ADD SmallestUnit nvarchar(50) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
                  AND c.name = N'SmallestUnit'
                  AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 100
                       OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.StockStaging ALTER COLUMN SmallestUnit nvarchar(50) NULL;

            IF COL_LENGTH(N'dbo.StockStaging', N'SmallestUnitQty') IS NULL
                ALTER TABLE dbo.StockStaging ADD SmallestUnitQty decimal(18,2) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
                  AND c.name = N'SmallestUnitQty'
                  AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                       OR c.scale <> 2 OR c.is_nullable <> 1)
            )
                ALTER TABLE dbo.StockStaging ALTER COLUMN SmallestUnitQty decimal(18,2) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Columns are retained because any of them may predate this migration.
    }
}
