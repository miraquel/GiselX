CREATE OR ALTER PROCEDURE [dbo].[GiselStock_SelectByCust]
    @CompanyId INT
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
    WHERE [CompanyId] = @CompanyId;
END
