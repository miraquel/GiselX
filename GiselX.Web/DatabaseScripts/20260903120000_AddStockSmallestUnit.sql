SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260903120000-AddStockSmallestUnit',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Stock SmallestUnit deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing Stock SmallestUnit.', 1;

    IF OBJECT_ID(N'dbo.Stock', N'U') IS NULL
        THROW 51004, 'dbo.Stock is required before installing Stock SmallestUnit.', 1;

    IF OBJECT_ID(N'dbo.StockStaging', N'U') IS NULL
        THROW 51005, 'dbo.StockStaging is required before installing Stock SmallestUnit.', 1;

    IF COL_LENGTH(N'dbo.Stock', N'SmallestUnit') IS NULL
        ALTER TABLE dbo.Stock ADD SmallestUnit nvarchar(50) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
          AND c.name = N'SmallestUnit'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 100
               OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Stock ALTER COLUMN SmallestUnit nvarchar(50) NULL;

    IF COL_LENGTH(N'dbo.Stock', N'SmallestUnitQty') IS NULL
        ALTER TABLE dbo.Stock ADD SmallestUnitQty decimal(18,2) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
          AND c.name = N'SmallestUnitQty'
          AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
               OR c.scale <> 2 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Stock ALTER COLUMN SmallestUnitQty decimal(18,2) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'SmallestUnit') IS NULL
        ALTER TABLE dbo.StockStaging ADD SmallestUnit nvarchar(50) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
          AND c.name = N'SmallestUnit'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 100
               OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN SmallestUnit nvarchar(50) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'SmallestUnitQty') IS NULL
        ALTER TABLE dbo.StockStaging ADD SmallestUnitQty decimal(18,2) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
          AND c.name = N'SmallestUnitQty'
          AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
               OR c.scale <> 2 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN SmallestUnitQty decimal(18,2) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260903120000_AddStockSmallestUnit'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260903120000_AddStockSmallestUnit', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Stock SmallestUnit / SmallestUnitQty installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
