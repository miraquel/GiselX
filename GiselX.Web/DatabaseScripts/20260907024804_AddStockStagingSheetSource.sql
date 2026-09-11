SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260907024804-AddStockStagingSheetSource',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Stock staging sheet source deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing Stock staging sheet source.', 1;

    IF OBJECT_ID(N'dbo.StockStaging', N'U') IS NULL
        THROW 51004, 'dbo.StockStaging is required before installing Stock staging sheet source.', 1;

    IF OBJECT_ID(N'dbo.StockTemplateMapping', N'U') IS NULL
        THROW 51005, 'dbo.StockTemplateMapping is required before installing Stock staging sheet source.', 1;

    IF COL_LENGTH(N'dbo.StockStaging', N'SourceWorksheetName') IS NULL
        ALTER TABLE dbo.StockStaging ADD SourceWorksheetName nvarchar(255) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
          AND c.name = N'SourceWorksheetName'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 510
               OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN SourceWorksheetName nvarchar(255) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'StockTemplateMappingId') IS NULL
        ALTER TABLE dbo.StockStaging ADD StockTemplateMappingId int NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
          AND c.name = N'StockTemplateMappingId'
          AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN StockTemplateMappingId int NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_StockStaging_StockTemplateMappingId'
          AND object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
    )
        CREATE INDEX IX_StockStaging_StockTemplateMappingId
            ON dbo.StockStaging (StockTemplateMappingId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_StockStaging_StockTemplateMapping_StockTemplateMappingId'
          AND parent_object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
    )
        ALTER TABLE dbo.StockStaging
            ADD CONSTRAINT FK_StockStaging_StockTemplateMapping_StockTemplateMappingId
            FOREIGN KEY (StockTemplateMappingId) REFERENCES dbo.StockTemplateMapping (Id)
            ON DELETE NO ACTION;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260907024804_AddStockStagingSheetSource'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260907024804_AddStockStagingSheetSource', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Stock staging sheet source installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
