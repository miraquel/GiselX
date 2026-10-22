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
    ORDER BY [InvoiceDate], [InvoiceNo], [Id];
END
