CREATE OR ALTER PROCEDURE [dbo].[GiselSales_del]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DELETE FROM [dbo].[SalesTransaction]
    WHERE [Id] = @Id;
END
