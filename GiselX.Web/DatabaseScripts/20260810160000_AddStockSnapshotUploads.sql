/*
    Stock snapshot upload schema deployment.

    Run this script in SSMS while connected to the intended GiselX database.
    The script is safe to rerun and does not modify or backfill existing dbo.Stock rows.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ApplicationLockResult int;

    EXEC @ApplicationLockResult = sys.sp_getapplock
        @Resource = N'GiselX_AddStockSnapshotUploads_20260810160000',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 60000;

    IF @ApplicationLockResult < 0
        THROW 51000, N'Could not acquire the Stock snapshot deployment lock.', 1;

    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
        THROW 51001, N'Required table dbo.Company does not exist.', 1;

    IF OBJECT_ID(N'dbo.Stock', N'U') IS NULL
        THROW 51002, N'Required table dbo.Stock does not exist.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51009, N'Required EF Core table dbo.__EFMigrationsHistory does not exist.', 1;

    IF COL_LENGTH(N'dbo.Stock', N'StockUploadBatchId') IS NULL
    BEGIN
        ALTER TABLE dbo.Stock ADD StockUploadBatchId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.Stock', N'StockDate') IS NULL
    BEGIN
        ALTER TABLE dbo.Stock ADD StockDate date NULL;
    END;

    IF OBJECT_ID(N'dbo.StockTemplateMapping', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockTemplateMapping
        (
            Id int IDENTITY(1, 1) NOT NULL,
            CompanyId int NOT NULL,
            Name nvarchar(100) NOT NULL,
            WorksheetName nvarchar(128) NOT NULL,
            HeaderRowNumber int NOT NULL,
            FirstDataRowNumber int NOT NULL,
            HeaderSignature nvarchar(max) NOT NULL,
            IsActive bit NOT NULL,
            CreatedDate datetime2 NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            ModifiedDate datetime2 NULL,
            ModifiedBy nvarchar(256) NULL
        );
    END;

    IF OBJECT_ID(N'dbo.StockTemplateColumn', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockTemplateColumn
        (
            Id int IDENTITY(1, 1) NOT NULL,
            StockTemplateMappingId int NOT NULL,
            SourceColumnNumber int NOT NULL,
            SourceHeader nvarchar(500) NOT NULL,
            CanonicalField int NOT NULL
        );
    END;

    IF OBJECT_ID(N'dbo.StockUploadBatch', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockUploadBatch
        (
            Id bigint IDENTITY(1, 1) NOT NULL,
            CompanyId int NOT NULL,
            StockTemplateMappingId int NULL,
            StockDate date NOT NULL,
            OriginalFileName nvarchar(260) NOT NULL,
            FileSha256 nchar(64) NOT NULL,
            Status int NOT NULL,
            TotalRows int NOT NULL,
            ValidRows int NOT NULL,
            InvalidRows int NOT NULL,
            IgnoredSummaryRowsJson nvarchar(max) NOT NULL,
            ValidationErrorsJson nvarchar(max) NOT NULL,
            ErrorSummary nvarchar(max) NULL,
            UploadedBy nvarchar(256) NOT NULL,
            UploadedDate datetime2 NOT NULL,
            CompletedDate datetime2 NULL
        );
    END;

    IF OBJECT_ID(N'dbo.StockStaging', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockStaging
        (
            Id bigint IDENTITY(1, 1) NOT NULL,
            StockUploadBatchId bigint NOT NULL,
            SourceRowNumber int NOT NULL,
            ProductId nvarchar(100) NULL,
            ProductName nvarchar(200) NULL,
            ProductPackaging nvarchar(100) NULL,
            ProductPcsInCtn int NULL,
            ProductNetto decimal(18, 2) NULL,
            ProductUnit nvarchar(50) NULL,
            SaldoAwal decimal(18, 2) NULL,
            SaldoMasukPO decimal(18, 2) NULL,
            SaldoAkhir decimal(18, 2) NULL,
            BatchNumber nvarchar(100) NULL,
            ExpiredDate datetime NULL,
            IsValid bit NOT NULL,
            ValidationErrorsJson nvarchar(max) NOT NULL
        );
    END;

    /* Fail safely when a prior partial deployment has an incompatible column. */
    DECLARE @RequiredColumns TABLE
    (
        TableName sysname NOT NULL,
        ColumnName sysname NOT NULL,
        TypeName sysname NOT NULL,
        ExpectedMaxLength smallint NULL,
        ExpectedPrecision tinyint NULL,
        ExpectedScale tinyint NULL,
        ExpectedNullable bit NOT NULL
    );

    INSERT INTO @RequiredColumns
        (TableName, ColumnName, TypeName, ExpectedMaxLength, ExpectedPrecision, ExpectedScale, ExpectedNullable)
    VALUES
        (N'Stock', N'StockUploadBatchId', N'bigint', NULL, NULL, NULL, 1),
        (N'Stock', N'StockDate', N'date', NULL, NULL, NULL, 1),
        (N'StockTemplateMapping', N'Id', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateMapping', N'CompanyId', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateMapping', N'Name', N'nvarchar', 200, NULL, NULL, 0),
        (N'StockTemplateMapping', N'WorksheetName', N'nvarchar', 256, NULL, NULL, 0),
        (N'StockTemplateMapping', N'HeaderRowNumber', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateMapping', N'FirstDataRowNumber', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateMapping', N'HeaderSignature', N'nvarchar', -1, NULL, NULL, 0),
        (N'StockTemplateMapping', N'IsActive', N'bit', NULL, NULL, NULL, 0),
        (N'StockTemplateMapping', N'CreatedDate', N'datetime2', NULL, NULL, NULL, 0),
        (N'StockTemplateMapping', N'CreatedBy', N'nvarchar', 512, NULL, NULL, 0),
        (N'StockTemplateMapping', N'ModifiedDate', N'datetime2', NULL, NULL, NULL, 1),
        (N'StockTemplateMapping', N'ModifiedBy', N'nvarchar', 512, NULL, NULL, 1),
        (N'StockTemplateColumn', N'Id', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateColumn', N'StockTemplateMappingId', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateColumn', N'SourceColumnNumber', N'int', NULL, NULL, NULL, 0),
        (N'StockTemplateColumn', N'SourceHeader', N'nvarchar', 1000, NULL, NULL, 0),
        (N'StockTemplateColumn', N'CanonicalField', N'int', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'Id', N'bigint', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'CompanyId', N'int', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'StockTemplateMappingId', N'int', NULL, NULL, NULL, 1),
        (N'StockUploadBatch', N'StockDate', N'date', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'OriginalFileName', N'nvarchar', 520, NULL, NULL, 0),
        (N'StockUploadBatch', N'FileSha256', N'nchar', 128, NULL, NULL, 0),
        (N'StockUploadBatch', N'Status', N'int', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'TotalRows', N'int', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'ValidRows', N'int', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'InvalidRows', N'int', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'IgnoredSummaryRowsJson', N'nvarchar', -1, NULL, NULL, 0),
        (N'StockUploadBatch', N'ValidationErrorsJson', N'nvarchar', -1, NULL, NULL, 0),
        (N'StockUploadBatch', N'ErrorSummary', N'nvarchar', -1, NULL, NULL, 1),
        (N'StockUploadBatch', N'UploadedBy', N'nvarchar', 512, NULL, NULL, 0),
        (N'StockUploadBatch', N'UploadedDate', N'datetime2', NULL, NULL, NULL, 0),
        (N'StockUploadBatch', N'CompletedDate', N'datetime2', NULL, NULL, NULL, 1),
        (N'StockStaging', N'Id', N'bigint', NULL, NULL, NULL, 0),
        (N'StockStaging', N'StockUploadBatchId', N'bigint', NULL, NULL, NULL, 0),
        (N'StockStaging', N'SourceRowNumber', N'int', NULL, NULL, NULL, 0),
        (N'StockStaging', N'ProductId', N'nvarchar', 200, NULL, NULL, 1),
        (N'StockStaging', N'ProductName', N'nvarchar', 400, NULL, NULL, 1),
        (N'StockStaging', N'ProductPackaging', N'nvarchar', 200, NULL, NULL, 1),
        (N'StockStaging', N'ProductPcsInCtn', N'int', NULL, NULL, NULL, 1),
        (N'StockStaging', N'ProductNetto', N'decimal', NULL, 18, 2, 1),
        (N'StockStaging', N'ProductUnit', N'nvarchar', 100, NULL, NULL, 1),
        (N'StockStaging', N'SaldoAwal', N'decimal', NULL, 18, 2, 1),
        (N'StockStaging', N'SaldoMasukPO', N'decimal', NULL, 18, 2, 1),
        (N'StockStaging', N'SaldoAkhir', N'decimal', NULL, 18, 2, 1),
        (N'StockStaging', N'BatchNumber', N'nvarchar', 200, NULL, NULL, 1),
        (N'StockStaging', N'ExpiredDate', N'datetime', NULL, NULL, NULL, 1),
        (N'StockStaging', N'IsValid', N'bit', NULL, NULL, NULL, 0),
        (N'StockStaging', N'ValidationErrorsJson', N'nvarchar', -1, NULL, NULL, 0);

    DECLARE @ColumnMismatch nvarchar(2048);

    SELECT TOP (1)
        @ColumnMismatch = CONCAT(
            N'Existing column dbo.', required.TableName, N'.', required.ColumnName,
            N' is missing or incompatible with migration 20260810160000.')
    FROM @RequiredColumns AS required
    LEFT JOIN sys.columns AS actual
        ON actual.object_id = OBJECT_ID(N'dbo.' + required.TableName, N'U')
       AND actual.name = required.ColumnName
    WHERE actual.column_id IS NULL
       OR TYPE_NAME(actual.system_type_id) <> required.TypeName
       OR actual.is_nullable <> required.ExpectedNullable
       OR (required.ExpectedMaxLength IS NOT NULL AND actual.max_length <> required.ExpectedMaxLength)
       OR (required.ExpectedPrecision IS NOT NULL AND actual.precision <> required.ExpectedPrecision)
       OR (required.ExpectedScale IS NOT NULL AND actual.scale <> required.ExpectedScale);

    IF @ColumnMismatch IS NOT NULL
        THROW 51003, @ColumnMismatch, 1;

    IF COLUMNPROPERTY(OBJECT_ID(N'dbo.StockTemplateMapping'), N'Id', 'IsIdentity') <> 1
        THROW 51004, N'dbo.StockTemplateMapping.Id must be an IDENTITY column.', 1;

    IF COLUMNPROPERTY(OBJECT_ID(N'dbo.StockTemplateColumn'), N'Id', 'IsIdentity') <> 1
        THROW 51005, N'dbo.StockTemplateColumn.Id must be an IDENTITY column.', 1;

    IF COLUMNPROPERTY(OBJECT_ID(N'dbo.StockUploadBatch'), N'Id', 'IsIdentity') <> 1
        THROW 51006, N'dbo.StockUploadBatch.Id must be an IDENTITY column.', 1;

    IF COLUMNPROPERTY(OBJECT_ID(N'dbo.StockStaging'), N'Id', 'IsIdentity') <> 1
        THROW 51007, N'dbo.StockStaging.Id must be an IDENTITY column.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockTemplateMapping')
          AND name = N'PK_StockTemplateMapping'
          AND type = N'PK'
    )
    BEGIN
        ALTER TABLE dbo.StockTemplateMapping
            ADD CONSTRAINT PK_StockTemplateMapping PRIMARY KEY (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockTemplateColumn')
          AND name = N'PK_StockTemplateColumn'
          AND type = N'PK'
    )
    BEGIN
        ALTER TABLE dbo.StockTemplateColumn
            ADD CONSTRAINT PK_StockTemplateColumn PRIMARY KEY (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'PK_StockUploadBatch'
          AND type = N'PK'
    )
    BEGIN
        ALTER TABLE dbo.StockUploadBatch
            ADD CONSTRAINT PK_StockUploadBatch PRIMARY KEY (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockStaging')
          AND name = N'PK_StockStaging'
          AND type = N'PK'
    )
    BEGIN
        ALTER TABLE dbo.StockStaging
            ADD CONSTRAINT PK_StockStaging PRIMARY KEY (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'DF_StockUploadBatch_IgnoredSummaryRowsJson'
    )
    BEGIN
        ALTER TABLE dbo.StockUploadBatch
            ADD CONSTRAINT DF_StockUploadBatch_IgnoredSummaryRowsJson
            DEFAULT (N'[]') FOR IgnoredSummaryRowsJson;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'DF_StockUploadBatch_ValidationErrorsJson'
    )
    BEGIN
        ALTER TABLE dbo.StockUploadBatch
            ADD CONSTRAINT DF_StockUploadBatch_ValidationErrorsJson
            DEFAULT (N'[]') FOR ValidationErrorsJson;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockStaging')
          AND name = N'DF_StockStaging_ValidationErrorsJson'
    )
    BEGIN
        ALTER TABLE dbo.StockStaging
            ADD CONSTRAINT DF_StockStaging_ValidationErrorsJson
            DEFAULT (N'[]') FOR ValidationErrorsJson;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockTemplateMapping')
          AND name = N'CK_StockTemplateMapping_HeaderRow'
    )
    BEGIN
        ALTER TABLE dbo.StockTemplateMapping WITH CHECK
            ADD CONSTRAINT CK_StockTemplateMapping_HeaderRow
            CHECK (HeaderRowNumber > 0);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockTemplateMapping')
          AND name = N'CK_StockTemplateMapping_FirstDataRow'
    )
    BEGIN
        ALTER TABLE dbo.StockTemplateMapping WITH CHECK
            ADD CONSTRAINT CK_StockTemplateMapping_FirstDataRow
            CHECK (FirstDataRowNumber > HeaderRowNumber);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockTemplateMapping')
          AND name = N'FK_StockTemplateMapping_Company_CompanyId'
    )
    BEGIN
        ALTER TABLE dbo.StockTemplateMapping WITH CHECK
            ADD CONSTRAINT FK_StockTemplateMapping_Company_CompanyId
            FOREIGN KEY (CompanyId) REFERENCES dbo.Company (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockTemplateColumn')
          AND name = N'FK_StockTemplateColumn_StockTemplateMapping_StockTemplateMappingId'
    )
    BEGIN
        ALTER TABLE dbo.StockTemplateColumn WITH CHECK
            ADD CONSTRAINT FK_StockTemplateColumn_StockTemplateMapping_StockTemplateMappingId
            FOREIGN KEY (StockTemplateMappingId) REFERENCES dbo.StockTemplateMapping (Id)
            ON DELETE CASCADE;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'FK_StockUploadBatch_Company_CompanyId'
    )
    BEGIN
        ALTER TABLE dbo.StockUploadBatch WITH CHECK
            ADD CONSTRAINT FK_StockUploadBatch_Company_CompanyId
            FOREIGN KEY (CompanyId) REFERENCES dbo.Company (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'FK_StockUploadBatch_StockTemplateMapping_StockTemplateMappingId'
    )
    BEGIN
        ALTER TABLE dbo.StockUploadBatch WITH CHECK
            ADD CONSTRAINT FK_StockUploadBatch_StockTemplateMapping_StockTemplateMappingId
            FOREIGN KEY (StockTemplateMappingId) REFERENCES dbo.StockTemplateMapping (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.StockStaging')
          AND name = N'FK_StockStaging_StockUploadBatch_StockUploadBatchId'
    )
    BEGIN
        ALTER TABLE dbo.StockStaging WITH CHECK
            ADD CONSTRAINT FK_StockStaging_StockUploadBatch_StockUploadBatchId
            FOREIGN KEY (StockUploadBatchId) REFERENCES dbo.StockUploadBatch (Id)
            ON DELETE CASCADE;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.Stock')
          AND name = N'FK_Stock_StockUploadBatch_StockUploadBatchId'
    )
    BEGIN
        ALTER TABLE dbo.Stock WITH CHECK
            ADD CONSTRAINT FK_Stock_StockUploadBatch_StockUploadBatchId
            FOREIGN KEY (StockUploadBatchId) REFERENCES dbo.StockUploadBatch (Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Stock')
          AND name = N'IX_Stock_StockUploadBatchId'
    )
    BEGIN
        CREATE INDEX IX_Stock_StockUploadBatchId
            ON dbo.Stock (StockUploadBatchId);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockTemplateMapping')
          AND name = N'IX_StockTemplateMapping_CompanyId_Name'
    )
    BEGIN
        CREATE UNIQUE INDEX IX_StockTemplateMapping_CompanyId_Name
            ON dbo.StockTemplateMapping (CompanyId, Name);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockTemplateColumn')
          AND name = N'IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber'
    )
    BEGIN
        CREATE UNIQUE INDEX IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber
            ON dbo.StockTemplateColumn (StockTemplateMappingId, SourceColumnNumber);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockTemplateColumn')
          AND name = N'IX_StockTemplateColumn_StockTemplateMappingId_CanonicalField'
    )
    BEGIN
        CREATE UNIQUE INDEX IX_StockTemplateColumn_StockTemplateMappingId_CanonicalField
            ON dbo.StockTemplateColumn (StockTemplateMappingId, CanonicalField);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'IX_StockUploadBatch_CompanyId_CompletedDate_Id'
    )
    BEGIN
        CREATE INDEX IX_StockUploadBatch_CompanyId_CompletedDate_Id
            ON dbo.StockUploadBatch (CompanyId, CompletedDate, Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'IX_StockUploadBatch_CompanyId_StockDate'
    )
    BEGIN
        CREATE INDEX IX_StockUploadBatch_CompanyId_StockDate
            ON dbo.StockUploadBatch (CompanyId, StockDate);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'IX_StockUploadBatch_Status'
    )
    BEGIN
        CREATE INDEX IX_StockUploadBatch_Status
            ON dbo.StockUploadBatch (Status);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockUploadBatch')
          AND name = N'IX_StockUploadBatch_StockTemplateMappingId'
    )
    BEGIN
        CREATE INDEX IX_StockUploadBatch_StockTemplateMappingId
            ON dbo.StockUploadBatch (StockTemplateMappingId);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.StockStaging')
          AND name = N'IX_StockStaging_StockUploadBatchId_SourceRowNumber'
    )
    BEGIN
        CREATE INDEX IX_StockStaging_StockUploadBatchId_SourceRowNumber
            ON dbo.StockStaging (StockUploadBatchId, SourceRowNumber);
    END;

    DECLARE @RequiredIndexes TABLE
    (
        TableName sysname NOT NULL,
        IndexName sysname NOT NULL,
        ExpectedUnique bit NOT NULL,
        ExpectedKeyColumns nvarchar(1000) NOT NULL
    );

    INSERT INTO @RequiredIndexes (TableName, IndexName, ExpectedUnique, ExpectedKeyColumns)
    VALUES
        (N'Stock', N'IX_Stock_StockUploadBatchId', 0, N'StockUploadBatchId'),
        (N'StockTemplateMapping', N'IX_StockTemplateMapping_CompanyId_Name', 1, N'CompanyId,Name'),
        (N'StockTemplateColumn', N'IX_StockTemplateColumn_StockTemplateMappingId_SourceColumnNumber', 1, N'StockTemplateMappingId,SourceColumnNumber'),
        (N'StockTemplateColumn', N'IX_StockTemplateColumn_StockTemplateMappingId_CanonicalField', 1, N'StockTemplateMappingId,CanonicalField'),
        (N'StockUploadBatch', N'IX_StockUploadBatch_CompanyId_CompletedDate_Id', 0, N'CompanyId,CompletedDate,Id'),
        (N'StockUploadBatch', N'IX_StockUploadBatch_CompanyId_StockDate', 0, N'CompanyId,StockDate'),
        (N'StockUploadBatch', N'IX_StockUploadBatch_Status', 0, N'Status'),
        (N'StockUploadBatch', N'IX_StockUploadBatch_StockTemplateMappingId', 0, N'StockTemplateMappingId'),
        (N'StockStaging', N'IX_StockStaging_StockUploadBatchId_SourceRowNumber', 0, N'StockUploadBatchId,SourceRowNumber');

    DECLARE @IndexMismatch nvarchar(2048);

    SELECT TOP (1)
        @IndexMismatch = CONCAT(
            N'Existing index dbo.', required.TableName, N'.', required.IndexName,
            N' is missing or incompatible with migration 20260810160000.')
    FROM @RequiredIndexes AS required
    LEFT JOIN sys.indexes AS actual
        ON actual.object_id = OBJECT_ID(N'dbo.' + required.TableName, N'U')
       AND actual.name = required.IndexName
    OUTER APPLY
    (
        SELECT
            STRING_AGG(
                CASE WHEN indexColumn.key_ordinal > 0
                    THEN CONVERT(nvarchar(max), columnMetadata.name)
                END,
                N',')
                WITHIN GROUP (ORDER BY indexColumn.key_ordinal) AS KeyColumns,
            COALESCE(SUM(CASE WHEN indexColumn.is_included_column = 1 THEN 1 ELSE 0 END), 0)
                AS IncludedColumnCount
        FROM sys.index_columns AS indexColumn
        INNER JOIN sys.columns AS columnMetadata
            ON columnMetadata.object_id = indexColumn.object_id
           AND columnMetadata.column_id = indexColumn.column_id
        WHERE indexColumn.object_id = actual.object_id
          AND indexColumn.index_id = actual.index_id
    ) AS shape
    WHERE actual.index_id IS NULL
       OR actual.is_unique <> required.ExpectedUnique
       OR actual.is_disabled = 1
       OR actual.has_filter = 1
       OR shape.IncludedColumnCount <> 0
       OR shape.KeyColumns <> required.ExpectedKeyColumns;

    IF @IndexMismatch IS NOT NULL
        THROW 51008, @IndexMismatch, 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260810160000_AddStockSnapshotUploads'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260810160000_AddStockSnapshotUploads', N'10.0.8');
    END;

    COMMIT TRANSACTION;

    PRINT N'Stock snapshot upload schema installed successfully.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
