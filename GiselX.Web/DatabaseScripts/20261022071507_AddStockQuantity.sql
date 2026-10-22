SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Stock rows gain the quantity in cartons and in pieces, read from the one or two quantity columns a
-- distributor's file carries. Both are optional: a template maps QtyCarton, QtyPcs or both. Existing
-- rows keep NULL, which the Stock page shows as no quantity given.

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20261022071507-AddStockQuantity',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Stock quantity deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before adding the Stock quantity columns.', 1;

    IF OBJECT_ID(N'dbo.Stock', N'U') IS NULL OR OBJECT_ID(N'dbo.StockStaging', N'U') IS NULL
        THROW 51004, 'dbo.Stock and dbo.StockStaging are required before adding the Stock quantity columns.', 1;

    IF COL_LENGTH(N'dbo.Stock', N'QtyCarton') IS NULL
        ALTER TABLE dbo.Stock ADD QtyCarton decimal(18,4) NULL;
    IF COL_LENGTH(N'dbo.Stock', N'QtyPcs') IS NULL
        ALTER TABLE dbo.Stock ADD QtyPcs decimal(18,4) NULL;
    IF COL_LENGTH(N'dbo.StockStaging', N'QtyCarton') IS NULL
        ALTER TABLE dbo.StockStaging ADD QtyCarton decimal(18,4) NULL;
    IF COL_LENGTH(N'dbo.StockStaging', N'QtyPcs') IS NULL
        ALTER TABLE dbo.StockStaging ADD QtyPcs decimal(18,4) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20261022071507_AddStockQuantity'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20261022071507_AddStockQuantity', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Stock quantity installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
