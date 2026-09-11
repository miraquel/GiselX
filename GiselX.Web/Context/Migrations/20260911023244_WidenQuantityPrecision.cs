using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class WidenQuantityPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SmallestUnitQty",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoMasukPO",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAwal",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAkhir",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductPcsInCtn",
                table: "StockStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductNetto",
                table: "StockStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SmallestUnitQty",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoMasukPO",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAwal",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAkhir",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductPcsInCtn",
                table: "Stock",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductNetto",
                table: "Stock",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductPcsInCtn",
                table: "SelloutStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "InvoiceQty",
                table: "SelloutStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "InKg",
                table: "SelloutStaging",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductPcsInCtn",
                table: "SalesTransaction",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "InvoiceQty",
                table: "SalesTransaction",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "InKg",
                table: "SalesTransaction",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            // The paging procedures declare one parameter per filterable column, so the quantity
            // filters are rebuilt to match the columns they now compare against.
            migrationBuilder.Sql(IsolateProcedureBatch(GiselSalesSelect));
            migrationBuilder.Sql(IsolateProcedureBatch(GiselStockSelect));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SmallestUnitQty",
                table: "StockStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoMasukPO",
                table: "StockStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAwal",
                table: "StockStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAkhir",
                table: "StockStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductPcsInCtn",
                table: "StockStaging",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductNetto",
                table: "StockStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SmallestUnitQty",
                table: "Stock",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoMasukPO",
                table: "Stock",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAwal",
                table: "Stock",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SaldoAkhir",
                table: "Stock",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<int>(
                name: "ProductPcsInCtn",
                table: "Stock",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ProductNetto",
                table: "Stock",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<int>(
                name: "ProductPcsInCtn",
                table: "SelloutStaging",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "InvoiceQty",
                table: "SelloutStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "InKg",
                table: "SelloutStaging",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductPcsInCtn",
                table: "SalesTransaction",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "InvoiceQty",
                table: "SalesTransaction",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "InKg",
                table: "SalesTransaction",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            // The widened procedure parameters stay in place: a decimal parameter still compares
            // correctly against the narrower columns this rollback restores.
        }

        /// <summary>
        /// Wraps one CREATE OR ALTER batch so it can travel inside a migration, which sends its SQL as a
        /// single batch and therefore cannot use GO separators.
        /// </summary>
        private static string IsolateProcedureBatch(string sql) => $"EXEC(N'{sql.Replace("'", "''")}');";

        private const string GiselSalesSelect = """
CREATE OR ALTER PROCEDURE [dbo].[GiselSales_Select]
    -- Paging & sorting
    @CompanyId      INT,
    @UseDistributorIdentity BIT = 0,
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- Period filter (from year/month dropdowns)
    @Year           INT            = NULL,
    @Month          INT            = NULL,
    @SearchTerm     NVARCHAR(200)  = NULL,
    -- String column filters (support LIKE via %)
    @CustomerId         NVARCHAR(100)  = NULL,
    @CustomerName       NVARCHAR(200)  = NULL,
    @CustomerAlias      NVARCHAR(200)  = NULL,
    @CustomerAddress    NVARCHAR(500)  = NULL,
    @ProductId          NVARCHAR(100)  = NULL,
    @ProductBrand       NVARCHAR(200)  = NULL,
    @ProductName        NVARCHAR(200)  = NULL,
    @ProductPackaging   NVARCHAR(100)  = NULL,
    @BatchNumber        NVARCHAR(100)  = NULL,
    @InvoiceNo          NVARCHAR(100)  = NULL,
    @InvoiceUnit        NVARCHAR(50)   = NULL,
    @GeisaPOId          NVARCHAR(100)  = NULL,
    @SalesmanNameGMK    NVARCHAR(200)  = NULL,
    -- Decimal column filters
    @ProductPcsInCtn    DECIMAL(18, 2) = NULL,
    @InvoiceQty     DECIMAL(18, 4) = NULL,
    @UnitPrice      DECIMAL(18, 2) = NULL,
    @GrossValue     DECIMAL(18, 2) = NULL,
    @DiscountValue  DECIMAL(18, 2) = NULL,
    @DiscountPct    DECIMAL(18, 2) = NULL,
    @NetValue       DECIMAL(18, 2) = NULL,
    @InKg           DECIMAL(18, 4) = NULL,
    -- DateTime column filters
    @ExpiredDate    DATETIME       = NULL,
    @InvoiceDate    DATETIME       = NULL,
    @ShipDate       DATETIME       = NULL,
    @CreatedDate    DATETIME       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- ── DATA RESULT SET ─────────────────────────────────────────────────────────
    SELECT
        [Id],
        [CustomerId],
        [CustomerName],
        [CustomerAlias],
        [CustomerAddress],
        CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END AS [ProductId],
        [ProductBrand],
        CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END AS [ProductName],
        [ProductPackaging],
        [ProductPcsInCtn],
        [BatchNumber],
        [ExpiredDate],
        [InvoiceNo],
        [InvoiceDate],
        [InvoiceQty],
        [InvoiceUnit],
        [UnitPrice],
        [GrossValue],
        [DiscountValue],
        [DiscountPct],
        [NetValue],
        [InKg],
        [GeisaPOId],
        [ShipDate],
        [SalesmanNameGMK],
        [CompanyId],
        [CreatedDate]
    FROM [dbo].[SalesTransaction]
    WHERE [CompanyId] = @CompanyId
      -- Period
      AND (@Year        IS NULL OR YEAR([InvoiceDate])  = @Year)
      AND (@Month       IS NULL OR MONTH([InvoiceDate]) = @Month)
      -- Latest upload batch within the period
      AND [CreatedDate] = (
          SELECT MAX([CreatedDate])
          FROM [dbo].[SalesTransaction]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([InvoiceDate])  = @Year)
            AND (@Month IS NULL OR MONTH([InvoiceDate]) = @Month)
      )
      AND (@SearchTerm IS NULL OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END LIKE @SearchTerm OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END LIKE @SearchTerm)
      -- String filters
      AND (@CustomerId        IS NULL OR [CustomerId]       LIKE @CustomerId)
      AND (@CustomerName      IS NULL OR [CustomerName]     LIKE @CustomerName)
      AND (@CustomerAlias     IS NULL OR [CustomerAlias]    LIKE @CustomerAlias)
      AND (@CustomerAddress   IS NULL OR [CustomerAddress]  LIKE @CustomerAddress)
      AND (@ProductId         IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END LIKE @ProductId)
      AND (@ProductBrand      IS NULL OR [ProductBrand]     LIKE @ProductBrand)
      AND (@ProductName       IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END LIKE @ProductName)
      AND (@ProductPackaging  IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@BatchNumber       IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      AND (@InvoiceNo         IS NULL OR [InvoiceNo]        LIKE @InvoiceNo)
      AND (@InvoiceUnit       IS NULL OR [InvoiceUnit]      LIKE @InvoiceUnit)
      AND (@GeisaPOId         IS NULL OR [GeisaPOId]        LIKE @GeisaPOId)
      AND (@SalesmanNameGMK   IS NULL OR [SalesmanNameGMK]  LIKE @SalesmanNameGMK)
      -- Integer filters
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      -- Decimal filters
      AND (@InvoiceQty    IS NULL OR [InvoiceQty]    = @InvoiceQty)
      AND (@UnitPrice     IS NULL OR [UnitPrice]     = @UnitPrice)
      AND (@GrossValue    IS NULL OR [GrossValue]    = @GrossValue)
      AND (@DiscountValue IS NULL OR [DiscountValue] = @DiscountValue)
      AND (@DiscountPct   IS NULL OR [DiscountPct]   = @DiscountPct)
      AND (@NetValue      IS NULL OR [NetValue]      = @NetValue)
      AND (@InKg          IS NULL OR [InKg]          = @InKg)
      -- DateTime filters (exact date match, ignoring time)
      AND (@ExpiredDate  IS NULL OR CAST([ExpiredDate]  AS DATE) = CAST(@ExpiredDate  AS DATE))
      AND (@InvoiceDate  IS NULL OR CAST([InvoiceDate]  AS DATE) = CAST(@InvoiceDate  AS DATE))
      AND (@ShipDate     IS NULL OR CAST([ShipDate]     AS DATE) = CAST(@ShipDate     AS DATE))
      AND (@CreatedDate  IS NULL OR CAST([CreatedDate]  AS DATE) = CAST(@CreatedDate  AS DATE))
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('invoiceNo', 'InvoiceNo')
            THEN [InvoiceNo]       END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('customerId', 'CustomerId')
            THEN [CustomerId]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('customerName', 'CustomerName')
            THEN [CustomerName]    END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productId', 'ProductId')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productBrand', 'ProductBrand')
            THEN [ProductBrand]    END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productName', 'ProductName')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productPackaging', 'ProductPackaging')
            THEN [ProductPackaging] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('batchNumber', 'BatchNumber')
            THEN [BatchNumber]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('invoiceUnit', 'InvoiceUnit')
            THEN [InvoiceUnit]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('geisaPOId', 'GeisaPOId')
            THEN [GeisaPOId]       END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('salesmanNameGMK', 'SalesmanNameGMK')
            THEN [SalesmanNameGMK] END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('invoiceNo', 'InvoiceNo')
            THEN [InvoiceNo]       END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('customerId', 'CustomerId')
            THEN [CustomerId]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('customerName', 'CustomerName')
            THEN [CustomerName]    END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productId', 'ProductId')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productBrand', 'ProductBrand')
            THEN [ProductBrand]    END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productName', 'ProductName')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productPackaging', 'ProductPackaging')
            THEN [ProductPackaging] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('batchNumber', 'BatchNumber')
            THEN [BatchNumber]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('invoiceUnit', 'InvoiceUnit')
            THEN [InvoiceUnit]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('geisaPOId', 'GeisaPOId')
            THEN [GeisaPOId]       END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('salesmanNameGMK', 'SalesmanNameGMK')
            THEN [SalesmanNameGMK] END DESC,
        -- DateTime columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('invoiceDate', 'InvoiceDate')
            THEN [InvoiceDate]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('expiredDate', 'ExpiredDate')
            THEN [ExpiredDate]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('shipDate', 'ShipDate')
            THEN [ShipDate]        END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]     END ASC,
        -- DateTime columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('invoiceDate', 'InvoiceDate')
            THEN [InvoiceDate]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('expiredDate', 'ExpiredDate')
            THEN [ExpiredDate]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('shipDate', 'ShipDate')
            THEN [ShipDate]        END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]     END DESC,
        -- Decimal columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('invoiceQty', 'InvoiceQty')
            THEN [InvoiceQty]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('unitPrice', 'UnitPrice')
            THEN [UnitPrice]       END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('grossValue', 'GrossValue')
            THEN [GrossValue]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('discountValue', 'DiscountValue')
            THEN [DiscountValue]   END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('discountPct', 'DiscountPct')
            THEN [DiscountPct]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('netValue', 'NetValue')
            THEN [NetValue]        END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('inKg', 'InKg')
            THEN [InKg]            END ASC,
        -- Decimal columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('invoiceQty', 'InvoiceQty')
            THEN [InvoiceQty]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('unitPrice', 'UnitPrice')
            THEN [UnitPrice]       END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('grossValue', 'GrossValue')
            THEN [GrossValue]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('discountValue', 'DiscountValue')
            THEN [DiscountValue]   END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('discountPct', 'DiscountPct')
            THEN [DiscountPct]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('netValue', 'NetValue')
            THEN [NetValue]        END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('inKg', 'InKg')
            THEN [InKg]            END DESC,
        -- Integer columns ASC/DESC
        CASE WHEN @SortOrder = 'ASC'  AND @SortColumn IN ('productPcsInCtn', 'ProductPcsInCtn')
            THEN [ProductPcsInCtn] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productPcsInCtn', 'ProductPcsInCtn')
            THEN [ProductPcsInCtn] END DESC,
        -- Default fallback
        [InvoiceDate] DESC,
        [InvoiceNo]   ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- ── TOTAL COUNT RESULT SET ───────────────────────────────────────────────────
    SELECT COUNT(*)
    FROM [dbo].[SalesTransaction]
    WHERE [CompanyId] = @CompanyId
      AND (@Year        IS NULL OR YEAR([InvoiceDate])  = @Year)
      AND (@Month       IS NULL OR MONTH([InvoiceDate]) = @Month)
      AND [CreatedDate] = (
          SELECT MAX([CreatedDate])
          FROM [dbo].[SalesTransaction]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([InvoiceDate])  = @Year)
            AND (@Month IS NULL OR MONTH([InvoiceDate]) = @Month)
      )
      AND (@SearchTerm IS NULL OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END LIKE @SearchTerm OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END LIKE @SearchTerm)
      AND (@CustomerId        IS NULL OR [CustomerId]       LIKE @CustomerId)
      AND (@CustomerName      IS NULL OR [CustomerName]     LIKE @CustomerName)
      AND (@CustomerAlias     IS NULL OR [CustomerAlias]    LIKE @CustomerAlias)
      AND (@CustomerAddress   IS NULL OR [CustomerAddress]  LIKE @CustomerAddress)
      AND (@ProductId         IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END LIKE @ProductId)
      AND (@ProductBrand      IS NULL OR [ProductBrand]     LIKE @ProductBrand)
      AND (@ProductName       IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END LIKE @ProductName)
      AND (@ProductPackaging  IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@BatchNumber       IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      AND (@InvoiceNo         IS NULL OR [InvoiceNo]        LIKE @InvoiceNo)
      AND (@InvoiceUnit       IS NULL OR [InvoiceUnit]      LIKE @InvoiceUnit)
      AND (@GeisaPOId         IS NULL OR [GeisaPOId]        LIKE @GeisaPOId)
      AND (@SalesmanNameGMK   IS NULL OR [SalesmanNameGMK]  LIKE @SalesmanNameGMK)
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      AND (@InvoiceQty    IS NULL OR [InvoiceQty]    = @InvoiceQty)
      AND (@UnitPrice     IS NULL OR [UnitPrice]     = @UnitPrice)
      AND (@GrossValue    IS NULL OR [GrossValue]    = @GrossValue)
      AND (@DiscountValue IS NULL OR [DiscountValue] = @DiscountValue)
      AND (@DiscountPct   IS NULL OR [DiscountPct]   = @DiscountPct)
      AND (@NetValue      IS NULL OR [NetValue]      = @NetValue)
      AND (@InKg          IS NULL OR [InKg]          = @InKg)
      AND (@ExpiredDate  IS NULL OR CAST([ExpiredDate]  AS DATE) = CAST(@ExpiredDate  AS DATE))
      AND (@InvoiceDate  IS NULL OR CAST([InvoiceDate]  AS DATE) = CAST(@InvoiceDate  AS DATE))
      AND (@ShipDate     IS NULL OR CAST([ShipDate]     AS DATE) = CAST(@ShipDate     AS DATE))
      AND (@CreatedDate  IS NULL OR CAST([CreatedDate]  AS DATE) = CAST(@CreatedDate  AS DATE));
END
""";

        private const string GiselStockSelect = """
CREATE OR ALTER PROCEDURE [dbo].[GiselStock_Select]
    -- Paging & sorting
    @CompanyId      INT,
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- Period filter (from year/month dropdowns, based on CreatedDate)
    @Year           INT            = NULL,
    @Month          INT            = NULL,
    -- String column filters (support LIKE via %)
    @ProductId          NVARCHAR(100)  = NULL,
    @ProductName        NVARCHAR(200)  = NULL,
    @ProductPackaging   NVARCHAR(100)  = NULL,
    @ProductUnit        NVARCHAR(50)   = NULL,
    @BatchNumber        NVARCHAR(100)  = NULL,
    -- Decimal column filters
    @ProductPcsInCtn    DECIMAL(18, 2) = NULL,
    @ProductNetto   DECIMAL(18, 4) = NULL,
    @PriceUnit      DECIMAL(18, 2) = NULL,
    @SaldoAwal      DECIMAL(18, 4) = NULL,
    @SaldoMasukPO   DECIMAL(18, 4) = NULL,
    @SaldoAkhir     DECIMAL(18, 4) = NULL,
    -- DateTime column filters
    @ExpiredDate    DATETIME       = NULL,
    @CreatedDate    DATETIME       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- ── DATA RESULT SET ─────────────────────────────────────────────────────────
    SELECT
        [Id],
        [ProductId],
        [ProductName],
        [ProductPackaging],
        [ProductPcsInCtn],
        [ProductNetto],
        [ProductUnit],
        [SmallestUnit],
        [SmallestUnitQty],
        [PriceUnit],
        [SaldoAwal],
        [SaldoMasukPO],
        [SaldoAkhir],
        [BatchNumber],
        [ExpiredDate],
        [CompanyId],
        [CreatedDate]
    FROM [dbo].[Stock]
    WHERE [CompanyId] = @CompanyId
      -- Period
      AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
      AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      -- Latest upload batch within the period
      AND CAST([CreatedDate] AS DATE) = (
          SELECT MAX(CAST([CreatedDate] AS DATE))
          FROM [dbo].[Stock]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
            AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      )
      -- String filters
      AND (@ProductId        IS NULL OR [ProductId]        LIKE @ProductId)
      AND (@ProductName      IS NULL OR [ProductName]      LIKE @ProductName)
      AND (@ProductPackaging IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@ProductUnit      IS NULL OR [ProductUnit]      LIKE @ProductUnit)
      AND (@BatchNumber      IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      -- Integer filters
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      -- Decimal filters
      AND (@ProductNetto IS NULL OR [ProductNetto] = @ProductNetto)
      AND (@PriceUnit    IS NULL OR [PriceUnit]    = @PriceUnit)
      AND (@SaldoAwal    IS NULL OR [SaldoAwal]    = @SaldoAwal)
      AND (@SaldoMasukPO IS NULL OR [SaldoMasukPO] = @SaldoMasukPO)
      AND (@SaldoAkhir   IS NULL OR [SaldoAkhir]   = @SaldoAkhir)
      -- DateTime filters (exact date match, ignoring time)
      AND (@ExpiredDate IS NULL OR CAST([ExpiredDate] AS DATE) = CAST(@ExpiredDate AS DATE))
      AND (@CreatedDate IS NULL OR CAST([CreatedDate] AS DATE) = CAST(@CreatedDate AS DATE))
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productId', 'ProductId')
            THEN [ProductId]        END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productName', 'ProductName')
            THEN [ProductName]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productPackaging', 'ProductPackaging')
            THEN [ProductPackaging] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productUnit', 'ProductUnit')
            THEN [ProductUnit]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('batchNumber', 'BatchNumber')
            THEN [BatchNumber]      END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productId', 'ProductId')
            THEN [ProductId]        END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productName', 'ProductName')
            THEN [ProductName]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productPackaging', 'ProductPackaging')
            THEN [ProductPackaging] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productUnit', 'ProductUnit')
            THEN [ProductUnit]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('batchNumber', 'BatchNumber')
            THEN [BatchNumber]      END DESC,
        -- DateTime columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('expiredDate', 'ExpiredDate')
            THEN [ExpiredDate]  END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]  END ASC,
        -- DateTime columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('expiredDate', 'ExpiredDate')
            THEN [ExpiredDate]  END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]  END DESC,
        -- Decimal columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productNetto', 'ProductNetto')
            THEN [ProductNetto] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('priceUnit', 'PriceUnit')
            THEN [PriceUnit] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('saldoAwal', 'SaldoAwal')
            THEN [SaldoAwal]    END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('saldoMasukPO', 'SaldoMasukPO')
            THEN [SaldoMasukPO] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('saldoAkhir', 'SaldoAkhir')
            THEN [SaldoAkhir]   END ASC,
        -- Decimal columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productNetto', 'ProductNetto')
            THEN [ProductNetto] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('priceUnit', 'PriceUnit')
            THEN [PriceUnit] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('saldoAwal', 'SaldoAwal')
            THEN [SaldoAwal]    END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('saldoMasukPO', 'SaldoMasukPO')
            THEN [SaldoMasukPO] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('saldoAkhir', 'SaldoAkhir')
            THEN [SaldoAkhir]   END DESC,
        -- Integer columns ASC/DESC
        CASE WHEN @SortOrder = 'ASC'  AND @SortColumn IN ('productPcsInCtn', 'ProductPcsInCtn')
            THEN [ProductPcsInCtn] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productPcsInCtn', 'ProductPcsInCtn')
            THEN [ProductPcsInCtn] END DESC,
        -- Default fallback
        [ProductId] ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- ── TOTAL COUNT RESULT SET ───────────────────────────────────────────────────
    SELECT COUNT(*)
    FROM [dbo].[Stock]
    WHERE [CompanyId] = @CompanyId
      AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
      AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      AND CAST([CreatedDate] AS DATE) = (
          SELECT MAX(CAST([CreatedDate] AS DATE))
          FROM [dbo].[Stock]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
            AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      )
      AND (@ProductId        IS NULL OR [ProductId]        LIKE @ProductId)
      AND (@ProductName      IS NULL OR [ProductName]      LIKE @ProductName)
      AND (@ProductPackaging IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@ProductUnit      IS NULL OR [ProductUnit]      LIKE @ProductUnit)
      AND (@BatchNumber      IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      AND (@ProductNetto IS NULL OR [ProductNetto] = @ProductNetto)
      AND (@PriceUnit    IS NULL OR [PriceUnit]    = @PriceUnit)
      AND (@SaldoAwal    IS NULL OR [SaldoAwal]    = @SaldoAwal)
      AND (@SaldoMasukPO IS NULL OR [SaldoMasukPO] = @SaldoMasukPO)
      AND (@SaldoAkhir   IS NULL OR [SaldoAkhir]   = @SaldoAkhir)
      AND (@ExpiredDate IS NULL OR CAST([ExpiredDate] AS DATE) = CAST(@ExpiredDate AS DATE))
      AND (@CreatedDate IS NULL OR CAST([CreatedDate] AS DATE) = CAST(@CreatedDate AS DATE));
END
""";
    }
}
