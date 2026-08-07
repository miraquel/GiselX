CREATE OR ALTER PROCEDURE [dbo].[Gisel_SelectByCustPeriod]
    @CreatedBy  NVARCHAR(256),
    @Year       INT,
    @Month      INT
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
    WHERE [CreatedBy]            = @CreatedBy
      AND YEAR([SoCreateDate])   = @Year
      AND MONTH([SoCreateDate])  = @Month;
END
