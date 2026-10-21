SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Installs GiselSales_SelectPeriods, which fills the Sales Transactions year and month pickers. The
-- application has always called it, but no migration created it, so a database built only from
-- migrations or these scripts lacked it. CREATE OR ALTER: a database where it was installed by hand
-- is brought to the same definition, and nothing else is touched. The Stock pickers use inline SQL.

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260929090000-AddSelectPeriodsProcedures',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the select-periods procedure deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing this procedure.', 1;

    IF OBJECT_ID(N'dbo.SalesTransaction', N'U') IS NULL
        THROW 51004, 'dbo.SalesTransaction is required.', 1;

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectPeriods]
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
END';

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260929090000_AddSelectPeriodsProcedures'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260929090000_AddSelectPeriodsProcedures', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Select-periods procedure installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
