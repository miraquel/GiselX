SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260810190000-AddStockPriceUnit',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Stock PriceUnit deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing Stock PriceUnit.', 1;

    IF COL_LENGTH(N'dbo.Stock', N'PriceUnit') IS NULL
        ALTER TABLE dbo.Stock ADD PriceUnit decimal(18,2) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
          AND c.name = N'PriceUnit'
          AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
               OR c.scale <> 2 OR c.is_nullable <> 1)
    )
        THROW 51000, 'dbo.Stock.PriceUnit must be nullable decimal(18,2).', 1;

    IF COL_LENGTH(N'dbo.StockStaging', N'PriceUnit') IS NULL
        ALTER TABLE dbo.StockStaging ADD PriceUnit decimal(18,2) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
          AND c.name = N'PriceUnit'
          AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
               OR c.scale <> 2 OR c.is_nullable <> 1)
    )
        THROW 51001, 'dbo.StockStaging.PriceUnit must be nullable decimal(18,2).', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260810190000_AddStockPriceUnit'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260810190000_AddStockPriceUnit', N'10.0.8');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
