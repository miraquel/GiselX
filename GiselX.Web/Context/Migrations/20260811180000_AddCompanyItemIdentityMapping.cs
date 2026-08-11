using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyItemIdentityMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistributorProductId",
                table: "StockStaging",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductName",
                table: "StockStaging",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemMappingId",
                table: "StockStaging",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductId",
                table: "Stock",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductName",
                table: "Stock",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemMappingId",
                table: "Stock",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductId",
                table: "SelloutStaging",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductName",
                table: "SelloutStaging",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemMappingId",
                table: "SelloutStaging",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductId",
                table: "SalesTransaction",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributorProductName",
                table: "SalesTransaction",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemMappingId",
                table: "SalesTransaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ItemMappingEnabled",
                table: "Company",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Historical rows retain their original GMK identity.  A mapping is deliberately
            // not inferred: only future uploads that resolve an active mapping receive one.
            migrationBuilder.Sql("""
                UPDATE dbo.Stock
                SET DistributorProductId = ProductId,
                    DistributorProductName = ProductName
                WHERE DistributorProductId IS NULL
                   OR DistributorProductName IS NULL;

                UPDATE dbo.SalesTransaction
                SET DistributorProductId = ProductId,
                    DistributorProductName = ProductName
                WHERE DistributorProductId IS NULL
                   OR DistributorProductName IS NULL;
                """);

            // The preceding PriceUnit migration was intentionally idempotent, but older
            // installations can contain an incompatible version of the column. Repair it
            // here instead of failing application startup.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.Stock', N'PriceUnit') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.columns c
                       WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
                         AND c.name = N'PriceUnit'
                         AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                              OR c.scale <> 2 OR c.is_nullable <> 1)
                   )
                    ALTER TABLE dbo.Stock ALTER COLUMN PriceUnit decimal(18,2) NULL;

                IF COL_LENGTH(N'dbo.StockStaging', N'PriceUnit') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.columns c
                       WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
                         AND c.name = N'PriceUnit'
                         AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                              OR c.scale <> 2 OR c.is_nullable <> 1)
                   )
                    ALTER TABLE dbo.StockStaging ALTER COLUMN PriceUnit decimal(18,2) NULL;
                """);

            migrationBuilder.Sql(CompanyInsertProcedure);
            migrationBuilder.Sql(CompanyUpdateProcedure);
            migrationBuilder.Sql(CompanySelectByIdProcedure);
            migrationBuilder.Sql(CompanySelectProcedure);
            migrationBuilder.Sql(SalesSelectProcedure);
            migrationBuilder.Sql(SalesSelectByCustPeriodProcedure);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "identity",
                table: "AspNetUserTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "identity",
                table: "AspNetUserTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "identity",
                table: "AspNetUserLogins",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "identity",
                table: "AspNetUserLogins",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.CreateTable(
                name: "ItemMapping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    GmkItemId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GmkItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DistributorItemId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DistributorItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemMapping_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockStaging_ItemMappingId",
                table: "StockStaging",
                column: "ItemMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_ItemMappingId",
                table: "Stock",
                column: "ItemMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_SelloutStaging_ItemMappingId",
                table: "SelloutStaging",
                column: "ItemMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransaction_ItemMappingId",
                table: "SalesTransaction",
                column: "ItemMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMapping_CompanyId_DistributorItemId",
                table: "ItemMapping",
                columns: new[] { "CompanyId", "DistributorItemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesTransaction_ItemMapping_ItemMappingId",
                table: "SalesTransaction",
                column: "ItemMappingId",
                principalTable: "ItemMapping",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SelloutStaging_ItemMapping_ItemMappingId",
                table: "SelloutStaging",
                column: "ItemMappingId",
                principalTable: "ItemMapping",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stock_ItemMapping_ItemMappingId",
                table: "Stock",
                column: "ItemMappingId",
                principalTable: "ItemMapping",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockStaging_ItemMapping_ItemMappingId",
                table: "StockStaging",
                column: "ItemMappingId",
                principalTable: "ItemMapping",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private const string CompanyInsertProcedure = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Company] (Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES (@Name, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = SCOPE_IDENTITY();
END
""";

        private const string CompanyUpdateProcedure = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id                 INT,
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Company]
    SET Name               = @Name,
        Address            = @Address,
        ContactEmail       = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth,
        DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays   = @ReminderLeadDays,
        ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
""";

        private const string CompanySelectByIdProcedure = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
""";

        private const string CompanySelectProcedure = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    -- Paging & sorting
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- String column filters (support LIKE via %)
    @Name           NVARCHAR(100)  = NULL,
    @Address        NVARCHAR(255)  = NULL,
    @ContactEmail   NVARCHAR(255)  = NULL,
    -- Integer column filters
    @Id                 INT        = NULL,
    @DeadlineDayOfMonth INT        = NULL,
    @DeadlineDaysOfWeek INT        = NULL,
    @ReminderLeadDays   INT        = NULL,
    @ItemMappingEnabled BIT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Ã¢â€â‚¬Ã¢â€â‚¬ DATA RESULT SET Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    SELECT
        [Id],
        [Name],
        [Address],
        [ContactEmail],
        [DeadlineDayOfMonth],
        [DeadlineDaysOfWeek],
        [ReminderLeadDays],
        [ItemMappingEnabled]
    FROM [dbo].[Company]
    WHERE 1 = 1
      -- String filters
      AND (@Name         IS NULL OR [Name]         LIKE @Name)
      AND (@Address      IS NULL OR [Address]      LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      -- Integer filters
      AND (@Id                 IS NULL OR [Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR [ReminderLeadDays]   = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled)
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name', 'Name')
            THEN [Name]         END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address', 'Address')
            THEN [Address]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('contactEmail', 'ContactEmail')
            THEN [ContactEmail] END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name', 'Name')
            THEN [Name]         END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address', 'Address')
            THEN [Address]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('contactEmail', 'ContactEmail')
            THEN [ContactEmail] END DESC,
        -- Integer columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('id', 'Id')
            THEN [Id]                 END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth')
            THEN [DeadlineDayOfMonth] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays')
            THEN [ReminderLeadDays]   END ASC,
        -- Integer columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('id', 'Id')
            THEN [Id]                 END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth')
            THEN [DeadlineDayOfMonth] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays')
            THEN [ReminderLeadDays]   END DESC,
        -- Default fallback
        [Name] ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Ã¢â€â‚¬Ã¢â€â‚¬ TOTAL COUNT RESULT SET Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    SELECT COUNT(*)
    FROM [dbo].[Company]
    WHERE 1 = 1
      AND (@Name         IS NULL OR [Name]         LIKE @Name)
      AND (@Address      IS NULL OR [Address]      LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id                 IS NULL OR [Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR [ReminderLeadDays]   = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled);
END
""";

        private const string SalesSelectProcedure = """
CREATE OR ALT…971 tokens truncated… -- Period
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

    -- Ã¢â€â‚¬Ã¢â€â‚¬ TOTAL COUNT RESULT SET Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
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

        private const string SalesSelectByCustPeriodProcedure = """
CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectByCustPeriod]
    @CompanyId INT,
    @Year INT,
    @Month INT,
    @UseDistributorIdentity BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

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
      AND YEAR([InvoiceDate]) = @Year
      AND MONTH([InvoiceDate]) = @Month
      AND [CreatedDate] = (
          SELECT MAX([CreatedDate])
          FROM [dbo].[SalesTransaction]
          WHERE [CompanyId] = @CompanyId
            AND YEAR([InvoiceDate]) = @Year
            AND MONTH([InvoiceDate]) = @Month
      )
    ORDER BY [InvoiceDate], [InvoiceNo], [Id];
END
""";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesTransaction_ItemMapping_ItemMappingId",
                table: "SalesTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_SelloutStaging_ItemMapping_ItemMappingId",
                table: "SelloutStaging");

            migrationBuilder.DropForeignKey(
                name: "FK_Stock_ItemMapping_ItemMappingId",
                table: "Stock");

            migrationBuilder.DropForeignKey(
                name: "FK_StockStaging_ItemMapping_ItemMappingId",
                table: "StockStaging");

            migrationBuilder.DropTable(
                name: "ItemMapping");

            migrationBuilder.DropIndex(
                name: "IX_StockStaging_ItemMappingId",
                table: "StockStaging");

            migrationBuilder.DropIndex(
                name: "IX_Stock_ItemMappingId",
                table: "Stock");

            migrationBuilder.DropIndex(
                name: "IX_SelloutStaging_ItemMappingId",
                table: "SelloutStaging");

            migrationBuilder.DropIndex(
                name: "IX_SalesTransaction_ItemMappingId",
                table: "SalesTransaction");

            migrationBuilder.DropColumn(
                name: "DistributorProductId",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "DistributorProductName",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "ItemMappingId",
                table: "StockStaging");

            migrationBuilder.DropColumn(
                name: "DistributorProductId",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "DistributorProductName",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "ItemMappingId",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "DistributorProductId",
                table: "SelloutStaging");

            migrationBuilder.DropColumn(
                name: "DistributorProductName",
                table: "SelloutStaging");

            migrationBuilder.DropColumn(
                name: "ItemMappingId",
                table: "SelloutStaging");

            migrationBuilder.DropColumn(
                name: "DistributorProductId",
                table: "SalesTransaction");

            migrationBuilder.DropColumn(
                name: "DistributorProductName",
                table: "SalesTransaction");

            migrationBuilder.DropColumn(
                name: "ItemMappingId",
                table: "SalesTransaction");

            migrationBuilder.DropColumn(
                name: "ItemMappingEnabled",
                table: "Company");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "identity",
                table: "AspNetUserTokens",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "identity",
                table: "AspNetUserTokens",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "identity",
                table: "AspNetUserLogins",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "identity",
                table: "AspNetUserLogins",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}




