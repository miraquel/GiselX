CREATE OR ALTER PROCEDURE [dbo].[GiselStock_SelectByCustPeriod]
    @CompanyId INT,
    @Year      INT,
    @Month     INT
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    SELECT
        [Id],
        [ProductId],
        [ProductName],
        [ProductPackaging],
        [ProductPcsInCtn],
        [ProductNetto],
        [ProductUnit],
        [SaldoAwal],
        [SaldoMasukPO],
        [SaldoAkhir],
        [BatchNumber],
        [ExpiredDate],
        [CompanyId],
        [CreatedDate]
    FROM [dbo].[Stock]
    WHERE [CompanyId] = @CompanyId
      AND YEAR([CreatedDate])  = @Year
      AND MONTH([CreatedDate]) = @Month;
END
