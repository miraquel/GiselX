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
