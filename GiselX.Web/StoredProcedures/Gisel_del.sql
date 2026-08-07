CREATE OR ALTER PROCEDURE [dbo].[Gisel_del]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DELETE FROM [dbo].[TransDist]
    WHERE [Id] = @Id;
END
