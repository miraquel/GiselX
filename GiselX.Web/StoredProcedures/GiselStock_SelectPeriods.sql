CREATE OR ALTER PROCEDURE [dbo].[GiselStock_SelectPeriods]
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    SELECT DISTINCT
        YEAR([CreatedDate])  AS [Year],
        MONTH([CreatedDate]) AS [Month]
    FROM [dbo].[Stock]
    WHERE [CompanyId] = @CompanyId
    ORDER BY [Year] DESC, [Month] DESC;
END
