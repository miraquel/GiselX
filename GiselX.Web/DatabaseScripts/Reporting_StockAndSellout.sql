/*
    Reporting procedures for published Stock and Sellout data.

    These are an ADD-ON for reporting and ad-hoc querying. Unlike the other files in this folder they
    are NOT a mirror of an EF migration: the application never calls them, and they deliberately do
    not write to dbo.__EFMigrationsHistory. Re-running this script is safe.

    Read this before writing your own query instead
    ----------------------------------------------
    Stock and Sellout behave DIFFERENTLY when a period is uploaded twice.

    Sellout  - SelloutUploadGroup promotion deletes the previous week's rows from SalesTransaction
               before inserting the new ones. The table therefore holds exactly one live version per
               company per reporting week, and a plain SELECT is correct.

    Stock    - publication only INSERTs. Nothing removes the rows of an earlier batch for the same
               snapshot date, and StockUploadBatchStatus has no "Replaced" value. Uploading the same
               StockDate twice leaves BOTH sets of rows in dbo.Stock, so

                   SELECT * FROM dbo.Stock WHERE StockDate = '2026-09-16'

               double-counts. GiselStock_SelectForReport below keeps only the most recently completed
               batch per company and snapshot date. Use it rather than reading dbo.Stock directly.

    Both procedures return row-level data. Aggregate in your own query or in Power BI.
*/

SET NOCOUNT ON;
GO

/* ------------------------------------------------------------------------------------------------
   Published Stock, de-duplicated to the latest completed batch per company and snapshot date.

   @CompanyId        NULL = every company.
   @FromDate/@ToDate NULL = unbounded. Filter on StockDate (the snapshot date), not the upload date.
   @IncludeLegacy    Rows uploaded through the old direct-import path carry no batch and no
                     StockDate. They cannot be de-duplicated and would be dropped by any date
                     filter, so they are excluded unless asked for explicitly.
------------------------------------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE [dbo].[GiselStock_SelectForReport]
    @CompanyId     INT  = NULL,
    @FromDate      DATE = NULL,
    @ToDate        DATE = NULL,
    @IncludeLegacy BIT  = 0
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH LiveBatch AS
    (
        -- Status 2 = Completed, 3 = CompletedWithWarnings. Note these differ from the Sellout enum.
        SELECT  b.Id,
                ROW_NUMBER() OVER (PARTITION BY b.CompanyId, b.StockDate
                                   ORDER BY b.CompletedDate DESC, b.Id DESC) AS Rank
        FROM    dbo.StockUploadBatch b
        WHERE   b.Status IN (2, 3)
    )
    SELECT  stock.Id,
            stock.CompanyId,
            company.Name                AS CompanyName,
            unit.Name                   AS BusinessUnitName,
            channel.Name                AS ChannelName,
            salesman.Name               AS SalesmanName,
            stock.StockDate,
            stock.ProductId,
            stock.ProductName,
            stock.DistributorProductId,
            stock.DistributorProductName,
            stock.ItemMappingId,
            stock.ProductPackaging,
            stock.ProductPcsInCtn,
            stock.ProductNetto,
            stock.ProductUnit,
            stock.SmallestUnit,
            stock.SmallestUnitQty,
            stock.PriceUnit,
            stock.SaldoAwal,
            stock.SaldoMasukPO,
            stock.SaldoAkhir,
            stock.BatchNumber,
            stock.ExpiredDate,
            stock.StockUploadBatchId,
            batch.OriginalFileName      AS UploadedFileName,
            batch.UploadedBy,
            batch.CompletedDate         AS UploadCompletedDate
    FROM    dbo.Stock stock
    JOIN    LiveBatch live       ON live.Id = stock.StockUploadBatchId AND live.Rank = 1
    JOIN    dbo.StockUploadBatch batch ON batch.Id = stock.StockUploadBatchId
    JOIN    dbo.Company company  ON company.Id = stock.CompanyId
    LEFT JOIN dbo.BusinessUnit unit     ON unit.Id     = company.BusinessUnitId
    LEFT JOIN dbo.Channel      channel  ON channel.Id  = company.ChannelId
    LEFT JOIN dbo.Salesman     salesman ON salesman.Id = company.SalesmanId
    WHERE   (@CompanyId IS NULL OR stock.CompanyId = @CompanyId)
      AND   (@FromDate  IS NULL OR stock.StockDate >= @FromDate)
      AND   (@ToDate    IS NULL OR stock.StockDate <= @ToDate)

    UNION ALL

    SELECT  stock.Id,
            stock.CompanyId,
            company.Name, unit.Name, channel.Name, salesman.Name,
            stock.StockDate,
            stock.ProductId, stock.ProductName,
            stock.DistributorProductId, stock.DistributorProductName, stock.ItemMappingId,
            stock.ProductPackaging, stock.ProductPcsInCtn, stock.ProductNetto, stock.ProductUnit,
            stock.SmallestUnit, stock.SmallestUnitQty, stock.PriceUnit,
            stock.SaldoAwal, stock.SaldoMasukPO, stock.SaldoAkhir,
            stock.BatchNumber, stock.ExpiredDate,
            stock.StockUploadBatchId,
            NULL, NULL, NULL
    FROM    dbo.Stock stock
    JOIN    dbo.Company company  ON company.Id = stock.CompanyId
    LEFT JOIN dbo.BusinessUnit unit     ON unit.Id     = company.BusinessUnitId
    LEFT JOIN dbo.Channel      channel  ON channel.Id  = company.ChannelId
    LEFT JOIN dbo.Salesman     salesman ON salesman.Id = company.SalesmanId
    WHERE   @IncludeLegacy = 1
      AND   stock.StockUploadBatchId IS NULL
      AND   (@CompanyId IS NULL OR stock.CompanyId = @CompanyId)

    ORDER BY CompanyName, StockDate, ProductId;
END
GO

/* ------------------------------------------------------------------------------------------------
   Published Sellout. SalesTransaction already holds one live version per reporting week, because
   re-uploading a week deletes the previous rows before inserting. The join to the batch is a
   safeguard and a label, not a de-duplication: it filters to live batches and carries the upload
   details through.

   @CompanyId        NULL = every company.
   @FromDate/@ToDate NULL = unbounded, filtered on InvoiceDate.
   @WeekStart        Optional exact reporting week (a Monday), when you want one week rather than a
                     date range that may straddle two.
------------------------------------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectForReport]
    @CompanyId     INT  = NULL,
    @FromDate      DATE = NULL,
    @ToDate        DATE = NULL,
    @WeekStart     DATE = NULL,
    @IncludeLegacy BIT  = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  sales.Id,
            sales.CompanyId,
            company.Name                AS CompanyName,
            unit.Name                   AS BusinessUnitName,
            channel.Name                AS ChannelName,
            salesman.Name               AS SalesmanName,
            sales.ReportingWeekStart,
            sales.InvoiceDate,
            sales.InvoiceNo,
            sales.CustomerId,
            sales.CustomerName,
            sales.CustomerAlias,
            sales.CustomerAddress,
            sales.ProductId,
            sales.ProductName,
            sales.ProductBrand,
            sales.DistributorProductId,
            sales.DistributorProductName,
            sales.ItemMappingId,
            sales.ProductPackaging,
            sales.ProductPcsInCtn,
            sales.BatchNumber,
            sales.ExpiredDate,
            sales.InvoiceQty,
            sales.InvoiceUnit,
            sales.UnitPrice,
            sales.GrossValue,
            sales.DiscountValue,
            sales.DiscountPct,
            sales.NetValue,
            sales.InKg,
            sales.GeisaPOId,
            sales.ShipDate,
            sales.SalesmanNameGMK,
            sales.SelloutUploadBatchId,
            batch.OriginalFileName      AS UploadedFileName,
            batch.UploadedBy,
            batch.CompletedDate         AS UploadCompletedDate
    FROM    dbo.SalesTransaction sales
    -- Status 2 = Completed, 4 = CompletedWithWarnings, 3 = Replaced. A replaced batch's rows are
    -- deleted on promotion, so this filter should never remove anything; it is here so a stale row
    -- from a future change cannot quietly enter a report.
    LEFT JOIN dbo.SelloutUploadBatch batch
           ON batch.Id = sales.SelloutUploadBatchId AND batch.Status IN (2, 4)
    JOIN    dbo.Company company  ON company.Id = sales.CompanyId
    LEFT JOIN dbo.BusinessUnit unit     ON unit.Id     = company.BusinessUnitId
    LEFT JOIN dbo.Channel      channel  ON channel.Id  = company.ChannelId
    LEFT JOIN dbo.Salesman     salesman ON salesman.Id = company.SalesmanId
    WHERE   (sales.SelloutUploadBatchId IS NULL AND @IncludeLegacy = 1
             OR batch.Id IS NOT NULL)
      AND   (@CompanyId IS NULL OR sales.CompanyId = @CompanyId)
      AND   (@WeekStart IS NULL OR sales.ReportingWeekStart = @WeekStart)
      AND   (@FromDate  IS NULL OR sales.InvoiceDate >= @FromDate)
      AND   (@ToDate    IS NULL OR sales.InvoiceDate <= @ToDate)
    ORDER BY CompanyName, sales.InvoiceDate, sales.InvoiceNo, sales.ProductId;
END
GO

PRINT 'Reporting procedures GiselStock_SelectForReport and GiselSales_SelectForReport installed.';
GO
