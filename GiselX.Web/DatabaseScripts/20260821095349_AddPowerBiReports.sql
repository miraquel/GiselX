SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260821095349-AddPowerBiReports',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51200, 'Could not acquire the Power BI reports deployment lock.', 1;

    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
        THROW 51201, 'dbo.Company must exist before installing Power BI reports.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        CREATE TABLE dbo.__EFMigrationsHistory
        (
            MigrationId nvarchar(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
            ProductVersion nvarchar(32) NOT NULL
        );

    IF OBJECT_ID(N'dbo.PowerBiReport', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PowerBiReport
        (
            Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PowerBiReport PRIMARY KEY,
            CompanyId int NOT NULL,
            Name nvarchar(150) NOT NULL,
            EmbedUrl nvarchar(2048) NOT NULL,
            DisplayOrder int NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_PowerBiReport_IsActive DEFAULT (1),
            CreatedDate datetime2 NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            ModifiedDate datetime2 NULL,
            ModifiedBy nvarchar(256) NULL,
            CONSTRAINT FK_PowerBiReport_Company_CompanyId
                FOREIGN KEY (CompanyId) REFERENCES dbo.Company(Id) ON DELETE NO ACTION
        );
    END
    ELSE IF EXISTS
    (
        SELECT 1
        FROM
        (
            VALUES
                (N'Id', N'int', 4, 0),
                (N'CompanyId', N'int', 4, 0),
                (N'Name', N'nvarchar', 300, 0),
                (N'EmbedUrl', N'nvarchar', 4096, 0),
                (N'DisplayOrder', N'int', 4, 0),
                (N'IsActive', N'bit', 1, 0),
                (N'CreatedDate', N'datetime2', 8, 0),
                (N'CreatedBy', N'nvarchar', 512, 0),
                (N'ModifiedDate', N'datetime2', 8, 1),
                (N'ModifiedBy', N'nvarchar', 512, 1)
        ) expected(Name, TypeName, MaxLength, IsNullable)
        LEFT JOIN sys.columns c ON c.object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U') AND c.name = expected.Name
        WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id) <> expected.TypeName
           OR c.max_length <> expected.MaxLength OR c.is_nullable <> expected.IsNullable
    )
        THROW 51202, 'dbo.PowerBiReport exists but is not the expected report schema.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND c.name = N'IsActive'
    )
        ALTER TABLE dbo.PowerBiReport
            ADD CONSTRAINT DF_PowerBiReport_IsActive DEFAULT (1) FOR IsActive;

    IF EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND c.name = N'IsActive'
          AND dc.definition NOT LIKE N'%1%'
    )
        THROW 51206, 'dbo.PowerBiReport.IsActive must default to true.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        INNER JOIN sys.columns parentColumn ON parentColumn.object_id = fkc.parent_object_id
            AND parentColumn.column_id = fkc.parent_column_id
        INNER JOIN sys.columns referencedColumn ON referencedColumn.object_id = fkc.referenced_object_id
            AND referencedColumn.column_id = fkc.referenced_column_id
        WHERE fk.parent_object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND fk.referenced_object_id = OBJECT_ID(N'dbo.Company', N'U')
          AND parentColumn.name = N'CompanyId'
          AND referencedColumn.name = N'Id'
          AND fk.delete_referential_action = 0
    )
        THROW 51203, 'dbo.PowerBiReport must have a restricted CompanyId foreign key.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND name = N'UX_PowerBiReport_CompanyId_Name'
    )
        CREATE UNIQUE INDEX UX_PowerBiReport_CompanyId_Name
            ON dbo.PowerBiReport(CompanyId, Name);
    ELSE IF EXISTS
    (
        SELECT 1
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND i.name = N'UX_PowerBiReport_CompanyId_Name'
          AND (i.is_unique <> 1 OR i.is_disabled <> 0 OR i.has_filter <> 0
               OR 2 <> (SELECT COUNT(*) FROM sys.index_columns ic
                         WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0)
               OR NOT EXISTS (SELECT 1 FROM sys.index_columns ic INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'CompanyId')
               OR NOT EXISTS (SELECT 1 FROM sys.index_columns ic INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal = 2 AND c.name = N'Name'))
    )
        THROW 51204, 'UX_PowerBiReport_CompanyId_Name exists but does not match the required unique index.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND name = N'IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name'
    )
        CREATE INDEX IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name
            ON dbo.PowerBiReport(CompanyId, IsActive, DisplayOrder, Name);
    ELSE IF EXISTS
    (
        SELECT 1
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'dbo.PowerBiReport', N'U')
          AND i.name = N'IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name'
          AND (i.is_unique <> 0 OR i.is_disabled <> 0 OR i.has_filter <> 0
               OR 4 <> (SELECT COUNT(*) FROM sys.index_columns ic
                         WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0)
               OR NOT EXISTS (SELECT 1 FROM sys.index_columns ic INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'CompanyId')
               OR NOT EXISTS (SELECT 1 FROM sys.index_columns ic INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal = 2 AND c.name = N'IsActive')
               OR NOT EXISTS (SELECT 1 FROM sys.index_columns ic INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal = 3 AND c.name = N'DisplayOrder')
               OR NOT EXISTS (SELECT 1 FROM sys.index_columns ic INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal = 4 AND c.name = N'Name'))
    )
        THROW 51205, 'IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name exists but does not match the required index.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260821095349_AddPowerBiReports'
    )
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260821095349_AddPowerBiReports', N'10.0.8');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
