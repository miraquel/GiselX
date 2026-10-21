SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20261021142701-AddStockAreaAndCondition',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Stock area and condition deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing Stock area and condition.', 1;

    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
        THROW 51004, 'dbo.Company is required before installing Stock area and condition.', 1;

    IF OBJECT_ID(N'dbo.Stock', N'U') IS NULL
        THROW 51005, 'dbo.Stock is required before installing Stock area and condition.', 1;

    IF OBJECT_ID(N'dbo.StockStaging', N'U') IS NULL
        THROW 51006, 'dbo.StockStaging is required before installing Stock area and condition.', 1;

    IF OBJECT_ID(N'dbo.StockTemplateMapping', N'U') IS NULL
        THROW 51007, 'dbo.StockTemplateMapping is required before installing Stock area and condition.', 1;

    -- The area master, one list per company.
    IF OBJECT_ID(N'dbo.StockArea', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockArea
        (
            Id int IDENTITY(1, 1) NOT NULL,
            CompanyId int NOT NULL,
            Name nvarchar(100) NOT NULL,
            Aliases nvarchar(1000) NULL,
            IsActive bit NOT NULL,
            CreatedDate datetime2 NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            ModifiedDate datetime2 NULL,
            ModifiedBy nvarchar(256) NULL,
            CONSTRAINT PK_StockArea PRIMARY KEY (Id),
            CONSTRAINT FK_StockArea_Company_CompanyId FOREIGN KEY (CompanyId)
                REFERENCES dbo.Company (Id) ON DELETE NO ACTION
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_StockArea_CompanyId_Name'
          AND object_id = OBJECT_ID(N'dbo.StockArea', N'U')
    )
        CREATE UNIQUE INDEX IX_StockArea_CompanyId_Name ON dbo.StockArea (CompanyId, Name);

    -- Stock: existing rows read Condition = 0 (Good) and no area.
    IF COL_LENGTH(N'dbo.Stock', N'StockAreaId') IS NULL
        ALTER TABLE dbo.Stock ADD StockAreaId int NULL;

    IF COL_LENGTH(N'dbo.Stock', N'Condition') IS NULL
        ALTER TABLE dbo.Stock ADD Condition tinyint NOT NULL DEFAULT (0);

    -- StockStaging
    IF COL_LENGTH(N'dbo.StockStaging', N'StockAreaId') IS NULL
        ALTER TABLE dbo.StockStaging ADD StockAreaId int NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'Condition') IS NULL
        ALTER TABLE dbo.StockStaging ADD Condition tinyint NOT NULL DEFAULT (0);

    IF COL_LENGTH(N'dbo.StockStaging', N'SourceAreaText') IS NULL
        ALTER TABLE dbo.StockStaging ADD SourceAreaText nvarchar(200) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'SourceConditionText') IS NULL
        ALTER TABLE dbo.StockStaging ADD SourceConditionText nvarchar(200) NULL;

    -- StockTemplateMapping
    IF COL_LENGTH(N'dbo.StockTemplateMapping', N'GoodConditionValues') IS NULL
        ALTER TABLE dbo.StockTemplateMapping ADD GoodConditionValues nvarchar(500) NULL;

    IF COL_LENGTH(N'dbo.StockTemplateMapping', N'BadConditionValues') IS NULL
        ALTER TABLE dbo.StockTemplateMapping ADD BadConditionValues nvarchar(500) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_Stock_StockAreaId'
          AND object_id = OBJECT_ID(N'dbo.Stock', N'U')
    )
        CREATE INDEX IX_Stock_StockAreaId ON dbo.Stock (StockAreaId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_StockStaging_StockAreaId'
          AND object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
    )
        CREATE INDEX IX_StockStaging_StockAreaId ON dbo.StockStaging (StockAreaId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_Stock_StockArea_StockAreaId'
          AND parent_object_id = OBJECT_ID(N'dbo.Stock', N'U')
    )
        ALTER TABLE dbo.Stock
            ADD CONSTRAINT FK_Stock_StockArea_StockAreaId
            FOREIGN KEY (StockAreaId) REFERENCES dbo.StockArea (Id)
            ON DELETE NO ACTION;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_StockStaging_StockArea_StockAreaId'
          AND parent_object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
    )
        ALTER TABLE dbo.StockStaging
            ADD CONSTRAINT FK_StockStaging_StockArea_StockAreaId
            FOREIGN KEY (StockAreaId) REFERENCES dbo.StockArea (Id)
            ON DELETE NO ACTION;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20261021142701_AddStockAreaAndCondition'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20261021142701_AddStockAreaAndCondition', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Stock area and condition installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
