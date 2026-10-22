/*
    Reporting objects for published Stock and Sellout data.

    Two views and two procedures. The views hold the logic; the procedures are thin filters over
    them, so both routes always agree.

        vw_GiselStock_Live            every live Stock row
        vw_GiselSales_Live            every live Sellout row
        GiselStock_SelectForReport    the same, filtered by company and snapshot date
        GiselSales_SelectForReport    the same, filtered by company, invoice date or reporting week

    For Power BI, point the dataset at the VIEWS. A view folds into the query Power BI sends, which
    keeps refresh incremental and lets filters push down to SQL Server; EXEC of a procedure does
    neither. Use the procedures for ad-hoc querying and for anything that wants parameters.

    This file is an add-on and NOT a mirror of an EF migration: the application never calls these,
    and nothing is written to dbo.__EFMigrationsHistory. Re-running the file is how it is updated.

    Why the views exist at all
    -------------------------
    Stock and Sellout used to behave differently when a period was uploaded twice. Sellout has
    always superseded: promoting a group deletes the published rows of the dates it contains before inserting. Stock
    only inserted, so re-uploading a snapshot date left BOTH sets of rows and every total was
    silently doubled.

    That is fixed at the source — publication now supersedes the way Sellout does. The views still
    de-duplicate, for two reasons: rows uploaded BEFORE the fix are still duplicated in dbo.Stock
    until Cleanup_DuplicateStockSnapshots.sql is run, and a report should not be the thing that
    breaks if a future change reintroduces the problem.
*/

SET NOCOUNT ON;
GO

/* ------------------------------------------------------------------------------------------------
   Live Stock: one row per product per company per snapshot date AND AREA, from the most recently
   completed batch that holds that area on that date. AreaName is NULL for rows without an area;
   Condition is 'Good' or 'Bad'. Rows from the old direct-import path carry no batch and no
   StockDate; they
   cannot be de-duplicated or filtered by date and are excluded here. Read dbo.Stock directly if you
   need them.
------------------------------------------------------------------------------------------------ */
CREATE OR ALTER VIEW [dbo].[vw_GiselStock_Live]
AS
    WITH LiveArea AS
    (
        -- One row per (live batch, area) actually present in dbo.Stock. Publication replaces only the
        -- areas an upload carries, so a company can have several live batches on one date (one per
        -- area) and a batch that lost an area to a later upload still ranks the areas it keeps. A row
        -- without an area counts as area 0, so a company without areas ranks one batch per date.
        -- Status 2 = Completed, 3 = CompletedWithWarnings, 4 = Replaced. These numbers are NOT the
        -- same as the Sellout enum, where 3 is Replaced and 4 is CompletedWithWarnings.
        SELECT  b.Id,
                d.AreaKey,
                ROW_NUMBER() OVER (PARTITION BY b.CompanyId, b.StockDate, COALESCE(d.StockAreaId,0)
                                   ORDER BY b.CompletedDate DESC, b.Id DESC) AS LiveRank
        FROM    dbo.StockUploadBatch b
        JOIN    (SELECT DISTINCT s.StockUploadBatchId, s.StockAreaId, COALESCE(s.StockAreaId,0) AS AreaKey
                 FROM   dbo.Stock s) d ON d.StockUploadBatchId = b.Id
        WHERE   b.Status IN (2, 3)
    )
    SELECT  stock.Id,
            stock.CompanyId,
            company.Name                AS CompanyName,
            company.Location            AS CompanyLocation,
            unit.Name                   AS BusinessUnitName,
            channel.Name                AS ChannelName,
            salesman.Name               AS SalesmanName,
            stock.StockDate,
            stock.StockAreaId,
            area.Name                   AS AreaName,
            CASE stock.Condition WHEN 1 THEN 'Bad' ELSE 'Good' END AS Condition,
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
    JOIN    LiveArea live              ON live.Id = stock.StockUploadBatchId AND live.AreaKey = COALESCE(stock.StockAreaId,0) AND live.LiveRank = 1
    JOIN    dbo.StockUploadBatch batch ON batch.Id = stock.StockUploadBatchId
    JOIN    dbo.Company company        ON company.Id = stock.CompanyId
    LEFT JOIN dbo.StockArea area         ON area.Id     = stock.StockAreaId
    LEFT JOIN dbo.BusinessUnit unit     ON unit.Id     = company.BusinessUnitId
    LEFT JOIN dbo.Channel      channel  ON channel.Id  = company.ChannelId
    LEFT JOIN dbo.Salesman     salesman ON salesman.Id = company.SalesmanId;
GO

/* ------------------------------------------------------------------------------------------------
   Live Sellout. A Sellout upload replaces the published rows of the invoice-date range it contains
   (earliest to latest valid invoice date), deleting the earlier rows in that range first, so
   SalesTransaction holds each date once however the uploads were cut into weeks. The batch join
   filters to live batches and carries the upload details through; it is a safeguard and a label,
   not a de-duplication.

   Status 2 = Completed, 4 = CompletedWithWarnings, 3 = Replaced — again, not the Stock numbering.
------------------------------------------------------------------------------------------------ */
CREATE OR ALTER VIEW [dbo].[vw_GiselSales_Live]
AS
    SELECT  sales.Id,
            sales.CompanyId,
            company.Name                AS CompanyName,
            company.Location            AS CompanyLocation,
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
    JOIN    dbo.SelloutUploadBatch batch ON batch.Id = sales.SelloutUploadBatchId
                                        AND batch.Status IN (2, 4)
    JOIN    dbo.Company company          ON company.Id = sales.CompanyId
    LEFT JOIN dbo.BusinessUnit unit     ON unit.Id     = company.BusinessUnitId
    LEFT JOIN dbo.Channel      channel  ON channel.Id  = company.ChannelId
    LEFT JOIN dbo.Salesman     salesman ON salesman.Id = company.SalesmanId;
GO

/* ------------------------------------------------------------------------------------------------
   Stock for a company and snapshot-date range. All parameters optional; NULL means unfiltered.
   @FromDate / @ToDate filter on StockDate, the date the stock position describes — not the day the
   file happened to be uploaded.
------------------------------------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE [dbo].[GiselStock_SelectForReport]
    @CompanyId INT  = NULL,
    @FromDate  DATE = NULL,
    @ToDate    DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  *
    FROM    dbo.vw_GiselStock_Live
    WHERE   (@CompanyId IS NULL OR CompanyId = @CompanyId)
      AND   (@FromDate  IS NULL OR StockDate >= @FromDate)
      AND   (@ToDate    IS NULL OR StockDate <= @ToDate)
    ORDER BY CompanyName, StockDate, ProductId;
END
GO

/* ------------------------------------------------------------------------------------------------
   Sellout for a company, by invoice-date range or by exact reporting week.

   @WeekStart is the Monday a reporting week begins on. Prefer it over a date range when you want
   whole weeks: a range chosen by calendar month will cut the weeks that straddle the month end.
------------------------------------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectForReport]
    @CompanyId INT  = NULL,
    @FromDate  DATE = NULL,
    @ToDate    DATE = NULL,
    @WeekStart DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  *
    FROM    dbo.vw_GiselSales_Live
    WHERE   (@CompanyId IS NULL OR CompanyId = @CompanyId)
      AND   (@WeekStart IS NULL OR ReportingWeekStart = @WeekStart)
      AND   (@FromDate  IS NULL OR InvoiceDate >= @FromDate)
      AND   (@ToDate    IS NULL OR InvoiceDate <= @ToDate)
    ORDER BY CompanyName, InvoiceDate, InvoiceNo, ProductId;
END
GO

PRINT 'Reporting views and procedures installed.';
GO
