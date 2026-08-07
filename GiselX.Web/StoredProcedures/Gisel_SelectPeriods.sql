CREATE OR ALTER PROCEDURE [dbo].[Gisel_SelectPeriods]
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    SELECT DISTINCT
        YEAR([SoCreateDate])  AS [Year],
        MONTH([SoCreateDate]) AS [Month]
    FROM [dbo].[TransDist]
    WHERE [CreatedBy] = @CreatedBy
    ORDER BY [Year] DESC, [Month] DESC;
END
