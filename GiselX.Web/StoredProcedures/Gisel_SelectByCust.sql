CREATE OR ALTER PROCEDURE [dbo].[Gisel_SelectByCust]
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    SELECT
        [Id],
        [SoId],
        [SoCreateDate],
        [LeadTimeDlv],
        [LeadTimeRct],
        [ItemId],
        [ItemName],
        [SoQty],
        [Unit],
        [KgPerUnit],
        [DlvDateRequest],
        [RctDateRequest],
        [DoQty],
        [DoDate],
        [ReceiptDate],
        [CreatedBy],
        [CreatedDate],
        [CompanyId]
    FROM [dbo].[TransDist]
    WHERE [CompanyId] = @CompanyId;
END
