SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260811180000-AddCompanyItemIdentityMapping',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51100, 'Could not acquire the company item mapping deployment lock.', 1;

    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Stock', N'U') IS NULL
       OR OBJECT_ID(N'dbo.StockStaging', N'U') IS NULL
       OR OBJECT_ID(N'dbo.SalesTransaction', N'U') IS NULL
       OR OBJECT_ID(N'dbo.SelloutStaging', N'U') IS NULL
        THROW 51101, 'The Company, Stock, StockStaging, SalesTransaction, and SelloutStaging tables must exist before this upgrade.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        CREATE TABLE dbo.__EFMigrationsHistory
        (
            MigrationId nvarchar(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
            ProductVersion nvarchar(32) NOT NULL
        );

    IF COL_LENGTH(N'dbo.Company', N'ItemMappingEnabled') IS NULL
    BEGIN
        ALTER TABLE dbo.Company
            ADD ItemMappingEnabled bit NOT NULL
                CONSTRAINT DF_Company_ItemMappingEnabled DEFAULT (0);
    END
    ELSE
    BEGIN
        UPDATE dbo.Company SET ItemMappingEnabled = 0 WHERE ItemMappingEnabled IS NULL;
        IF EXISTS
        (
            SELECT 1 FROM sys.columns c
            WHERE c.object_id = OBJECT_ID(N'dbo.Company', N'U')
              AND c.name = N'ItemMappingEnabled'
              AND (TYPE_NAME(c.user_type_id) <> N'bit' OR c.is_nullable <> 0)
        )
            ALTER TABLE dbo.Company ALTER COLUMN ItemMappingEnabled bit NOT NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Company', N'U')
          AND c.name = N'ItemMappingEnabled'
    )
        ALTER TABLE dbo.Company
            ADD CONSTRAINT DF_Company_ItemMappingEnabled DEFAULT (0) FOR ItemMappingEnabled;

    IF OBJECT_ID(N'dbo.ItemMapping', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ItemMapping
        (
            Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ItemMapping PRIMARY KEY,
            CompanyId int NOT NULL,
            GmkItemId nvarchar(100) NOT NULL,
            GmkItemName nvarchar(200) NOT NULL,
            DistributorItemId nvarchar(100) NOT NULL,
            DistributorItemName nvarchar(200) NOT NULL,
            IsActive bit NOT NULL,
            CreatedDate datetime2 NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            ModifiedDate datetime2 NULL,
            ModifiedBy nvarchar(256) NULL,
            CONSTRAINT FK_ItemMapping_Company_CompanyId
                FOREIGN KEY (CompanyId) REFERENCES dbo.Company(Id) ON DELETE NO ACTION
        );
    END
    ELSE IF EXISTS
    (
        SELECT 1
        FROM
        (
            VALUES
                (N'Id', N'int', 4, 0), (N'CompanyId', N'int', 4, 0),
                (N'GmkItemId', N'nvarchar', 200, 0), (N'GmkItemName', N'nvarchar', 400, 0),
                (N'DistributorItemId', N'nvarchar', 200, 0), (N'DistributorItemName', N'nvarchar', 400, 0),
                (N'IsActive', N'bit', 1, 0), (N'CreatedDate', N'datetime2', 8, 0),
                (N'CreatedBy', N'nvarchar', 512, 0), (N'ModifiedDate', N'datetime2', 8, 1),
                (N'ModifiedBy', N'nvarchar', 512, 1)
        ) expected(Name, TypeName, MaxLength, IsNullable)
        LEFT JOIN sys.columns c ON c.object_id = OBJECT_ID(N'dbo.ItemMapping', N'U') AND c.name = expected.Name
        WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id) <> expected.TypeName
           OR c.max_length <> expected.MaxLength OR c.is_nullable <> expected.IsNullable
    )
        THROW 51102, 'dbo.ItemMapping exists but is not the item mapping schema expected by this upgrade.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys fk
        WHERE fk.name = N'FK_ItemMapping_Company_CompanyId'
          AND fk.parent_object_id = OBJECT_ID(N'dbo.ItemMapping', N'U')
    )
        ALTER TABLE dbo.ItemMapping ADD CONSTRAINT FK_ItemMapping_Company_CompanyId
            FOREIGN KEY (CompanyId) REFERENCES dbo.Company(Id) ON DELETE NO ACTION;
    ELSE IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        INNER JOIN sys.columns parentColumn ON parentColumn.object_id = fkc.parent_object_id
            AND parentColumn.column_id = fkc.parent_column_id
        INNER JOIN sys.columns referencedColumn ON referencedColumn.object_id = fkc.referenced_object_id
            AND referencedColumn.column_id = fkc.referenced_column_id
        WHERE fk.name = N'FK_ItemMapping_Company_CompanyId'
          AND fk.parent_object_id = OBJECT_ID(N'dbo.ItemMapping', N'U')
          AND fk.referenced_object_id = OBJECT_ID(N'dbo.Company', N'U')
          AND parentColumn.name = N'CompanyId'
          AND referencedColumn.name = N'Id'
          AND fk.delete_referential_action = 0
    )
        THROW 51103, 'FK_ItemMapping_Company_CompanyId exists but is not the expected restrict CompanyId foreign key.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ItemMapping', N'U')
          AND name = N'IX_ItemMapping_CompanyId_DistributorItemId'
    )
        CREATE UNIQUE INDEX IX_ItemMapping_CompanyId_DistributorItemId
            ON dbo.ItemMapping(CompanyId, DistributorItemId);
    ELSE IF EXISTS
    (
        SELECT 1
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'dbo.ItemMapping', N'U')
          AND i.name = N'IX_ItemMapping_CompanyId_DistributorItemId'
          AND
          (
              i.is_unique <> 1
              OR i.has_filter <> 0
              OR i.is_disabled <> 0
              OR 2 <>
              (
                  SELECT COUNT(*)
                  FROM sys.index_columns ic
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
              )
              OR NOT EXISTS
              (
                  SELECT 1
                  FROM sys.index_columns ic
                  INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    AND ic.key_ordinal = 1 AND c.name = N'CompanyId'
              )
              OR NOT EXISTS
              (
                  SELECT 1
                  FROM sys.index_columns ic
                  INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    AND ic.key_ordinal = 2 AND c.name = N'DistributorItemId'
              )
          )
    )
        THROW 51104, 'IX_ItemMapping_CompanyId_DistributorItemId exists but is not the expected unique CompanyId/DistributorItemId index.', 1;

    IF COL_LENGTH(N'dbo.Stock', N'DistributorProductId') IS NULL
        ALTER TABLE dbo.Stock ADD DistributorProductId nvarchar(100) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U') AND c.name = N'DistributorProductId'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 200 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Stock ALTER COLUMN DistributorProductId nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.Stock', N'DistributorProductName') IS NULL
        ALTER TABLE dbo.Stock ADD DistributorProductName nvarchar(200) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U') AND c.name = N'DistributorProductName'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 400 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Stock ALTER COLUMN DistributorProductName nvarchar(200) NULL;

    IF COL_LENGTH(N'dbo.Stock', N'ItemMappingId') IS NULL
        ALTER TABLE dbo.Stock ADD ItemMappingId int NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U') AND c.name = N'ItemMappingId'
          AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Stock ALTER COLUMN ItemMappingId int NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'DistributorProductId') IS NULL
        ALTER TABLE dbo.StockStaging ADD DistributorProductId nvarchar(100) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U') AND c.name = N'DistributorProductId'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 200 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN DistributorProductId nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'DistributorProductName') IS NULL
        ALTER TABLE dbo.StockStaging ADD DistributorProductName nvarchar(200) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U') AND c.name = N'DistributorProductName'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 400 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN DistributorProductName nvarchar(200) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'ItemMappingId') IS NULL
        ALTER TABLE dbo.StockStaging ADD ItemMappingId int NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U') AND c.name = N'ItemMappingId'
          AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.StockStaging ALTER COLUMN ItemMappingId int NULL;

    IF COL_LENGTH(N'dbo.SalesTransaction', N'DistributorProductId') IS NULL
        ALTER TABLE dbo.SalesTransaction ADD DistributorProductId nvarchar(100) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.SalesTransaction', N'U') AND c.name = N'DistributorProductId'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 200 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.SalesTransaction ALTER COLUMN DistributorProductId nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.SalesTransaction', N'DistributorProductName') IS NULL
        ALTER TABLE dbo.SalesTransaction ADD DistributorProductName nvarchar(200) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.SalesTransaction', N'U') AND c.name = N'DistributorProductName'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 400 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.SalesTransaction ALTER COLUMN DistributorProductName nvarchar(200) NULL;

    IF COL_LENGTH(N'dbo.SalesTransaction', N'ItemMappingId') IS NULL
        ALTER TABLE dbo.SalesTransaction ADD ItemMappingId int NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.SalesTransaction', N'U') AND c.name = N'ItemMappingId'
          AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.SalesTransaction ALTER COLUMN ItemMappingId int NULL;

    IF COL_LENGTH(N'dbo.SelloutStaging', N'DistributorProductId') IS NULL
        ALTER TABLE dbo.SelloutStaging ADD DistributorProductId nvarchar(100) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.SelloutStaging', N'U') AND c.name = N'DistributorProductId'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 200 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.SelloutStaging ALTER COLUMN DistributorProductId nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.SelloutStaging', N'DistributorProductName') IS NULL
        ALTER TABLE dbo.SelloutStaging ADD DistributorProductName nvarchar(200) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.SelloutStaging', N'U') AND c.name = N'DistributorProductName'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 400 OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.SelloutStaging ALTER COLUMN DistributorProductName nvarchar(200) NULL;

    IF COL_LENGTH(N'dbo.SelloutStaging', N'ItemMappingId') IS NULL
        ALTER TABLE dbo.SelloutStaging ADD ItemMappingId int NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.SelloutStaging', N'U') AND c.name = N'ItemMappingId'
          AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.SelloutStaging ALTER COLUMN ItemMappingId int NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Stock', N'U') AND name = N'IX_Stock_ItemMappingId')
        CREATE INDEX IX_Stock_ItemMappingId ON dbo.Stock(ItemMappingId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockStaging', N'U') AND name = N'IX_StockStaging_ItemMappingId')
        CREATE INDEX IX_StockStaging_ItemMappingId ON dbo.StockStaging(ItemMappingId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SalesTransaction', N'U') AND name = N'IX_SalesTransaction_ItemMappingId')
        CREATE INDEX IX_SalesTransaction_ItemMappingId ON dbo.SalesTransaction(ItemMappingId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SelloutStaging', N'U') AND name = N'IX_SelloutStaging_ItemMappingId')
        CREATE INDEX IX_SelloutStaging_ItemMappingId ON dbo.SelloutStaging(ItemMappingId);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Stock_ItemMapping_ItemMappingId')
        ALTER TABLE dbo.Stock ADD CONSTRAINT FK_Stock_ItemMapping_ItemMappingId
            FOREIGN KEY (ItemMappingId) REFERENCES dbo.ItemMapping(Id) ON DELETE NO ACTION;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StockStaging_ItemMapping_ItemMappingId')
        ALTER TABLE dbo.StockStaging ADD CONSTRAINT FK_StockStaging_ItemMapping_ItemMappingId
            FOREIGN KEY (ItemMappingId) REFERENCES dbo.ItemMapping(Id) ON DELETE NO ACTION;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesTransaction_ItemMapping_ItemMappingId')
        ALTER TABLE dbo.SalesTransaction ADD CONSTRAINT FK_SalesTransaction_ItemMapping_ItemMappingId
            FOREIGN KEY (ItemMappingId) REFERENCES dbo.ItemMapping(Id) ON DELETE NO ACTION;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SelloutStaging_ItemMapping_ItemMappingId')
        ALTER TABLE dbo.SelloutStaging ADD CONSTRAINT FK_SelloutStaging_ItemMapping_ItemMappingId
            FOREIGN KEY (ItemMappingId) REFERENCES dbo.ItemMapping(Id) ON DELETE NO ACTION;

    UPDATE dbo.Stock
    SET DistributorProductId = ProductId, DistributorProductName = ProductName
    WHERE DistributorProductId IS NULL OR DistributorProductName IS NULL;

    UPDATE dbo.SalesTransaction
    SET DistributorProductId = ProductId, DistributorProductName = ProductName
    WHERE DistributorProductId IS NULL OR DistributorProductName IS NULL;

    IF COL_LENGTH(N'dbo.Stock', N'PriceUnit') IS NOT NULL
       AND EXISTS
       (
           SELECT 1 FROM sys.columns c
           WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U') AND c.name = N'PriceUnit'
             AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18 OR c.scale <> 2 OR c.is_nullable <> 1)
       )
        ALTER TABLE dbo.Stock ALTER COLUMN PriceUnit decimal(18,2) NULL;

    IF COL_LENGTH(N'dbo.StockStaging', N'PriceUnit') IS NOT NULL
       AND EXISTS
       (
           SELECT 1 FROM sys.columns c
           WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U') AND c.name = N'PriceUnit'
             AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18 OR c.scale <> 2 OR c.is_nullable <> 1)
       )
        ALTER TABLE dbo.StockStaging ALTER COLUMN PriceUnit decimal(18,2) NULL;

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Company] (Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES (@Name, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = SCOPE_IDENTITY();
END';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id                 INT,
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Company]
    SET Name               = @Name,
        Address            = @Address,
        ContactEmail       = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth,
        DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays   = @ReminderLeadDays,
        ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    -- Paging & sorting
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = ''ASC'',
    -- String column filters (support LIKE via %)
    @Name           NVARCHAR(100)  = NULL,
    @Address        NVARCHAR(255)  = NULL,
    @ContactEmail   NVARCHAR(255)  = NULL,
    -- Integer column filters
    @Id                 INT        = NULL,
    @DeadlineDayOfMonth INT        = NULL,
    @DeadlineDaysOfWeek INT        = NULL,
    @ReminderLeadDays   INT        = NULL,
    @ItemMappingEnabled BIT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- â”€â”€ DATA RESULT SET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    SELECT
        [Id],
        [Name],
        [Address],
        [ContactEmail],
        [DeadlineDayOfMonth],
        [DeadlineDaysOfWeek],
        [ReminderLeadDays],
        [ItemMappingEnabled]
    FROM [dbo].[Company]
    WHERE 1 = 1
      -- String filters
      AND (@Name         IS NULL OR [Name]         LIKE @Name)
      AND (@Address      IS NULL OR [Address]      LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      -- Integer filters
      AND (@Id                 IS NULL OR [Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR [ReminderLeadDays]   = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled)
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''name'', ''Name'')
            THEN [Name]         END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''address'', ''Address'')
            THEN [Address]      END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''contactEmail'', ''ContactEmail'')
            THEN [ContactEmail] END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''name'', ''Name'')
            THEN [Name]         END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''address'', ''Address'')
            THEN [Address]      END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''contactEmail'', ''ContactEmail'')
            THEN [ContactEmail] END DESC,
        -- Integer columns ASC
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''id'', ''Id'')
            THEN [Id]                 END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''deadlineDayOfMonth'', ''DeadlineDayOfMonth'')
            THEN [DeadlineDayOfMonth] END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''reminderLeadDays'', ''ReminderLeadDays'')
            THEN [ReminderLeadDays]   END ASC,
        -- Integer columns DESC
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''id'', ''Id'')
            THEN [Id]                 END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''deadlineDayOfMonth'', ''DeadlineDayOfMonth'')
            THEN [DeadlineDayOfMonth] END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''reminderLeadDays'', ''ReminderLeadDays'')
            THEN [ReminderLeadDays]   END DESC,
        -- Default fallback
        [Name] ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- â”€â”€ TOTAL COUNT RESULT SET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    SELECT COUNT(*)
    FROM [dbo].[Company]
    WHERE 1 = 1
      AND (@Name         IS NULL OR [Name]         LIKE @Name)
      AND (@Address      IS NULL OR [Address]      LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id                 IS NULL OR [Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR [ReminderLeadDays]   = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled);
END';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[GiselSales_Select]
    -- Paging & sorting
    @CompanyId      INT,
    @UseDistributorIdentity BIT = 0,
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = ''ASC'',
    -- Period filter (from year/month dropdowns)
    @Year           INT            = NULL,
    @Month          INT            = NULL,
    @SearchTerm     NVARCHAR(200)  = NULL,
    -- String column filters (support LIKE via %)
    @CustomerId         NVARCHAR(100)  = NULL,
    @CustomerName       NVARCHAR(200)  = NULL,
    @CustomerAlias      NVARCHAR(200)  = NULL,
    @CustomerAddress    NVARCHAR(500)  = NULL,
    @ProductId          NVARCHAR(100)  = NULL,
    @ProductBrand       NVARCHAR(200)  = NULL,
    @ProductName        NVARCHAR(200)  = NULL,
    @ProductPackaging   NVARCHAR(100)  = NULL,
    @BatchNumber        NVARCHAR(100)  = NULL,
    @InvoiceNo          NVARCHAR(100)  = NULL,
    @InvoiceUnit        NVARCHAR(50)   = NULL,
    @GeisaPOId          NVARCHAR(100)  = NULL,
    @SalesmanNameGMK    NVARCHAR(200)  = NULL,
    -- Integer column filters
    @ProductPcsInCtn    INT            = NULL,
    -- Decimal column filters
    @InvoiceQty     DECIMAL(18, 2) = NULL,
    @UnitPrice      DECIMAL(18, 2) = NULL,
    @GrossValue     DECIMAL(18, 2) = NULL,
    @DiscountValue  DECIMAL(18, 2) = NULL,
    @DiscountPct    DECIMAL(18, 2) = NULL,
    @NetValue       DECIMAL(18, 2) = NULL,
    @InKg           DECIMAL(18, 2) = NULL,
    -- DateTime column filters
    @ExpiredDate    DATETIME       = NULL,
    @InvoiceDate    DATETIME       = NULL,
    @ShipDate       DATETIME       = NULL,
    @CreatedDate    DATETIME       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- â”€â”€ DATA RESULT SET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    SELECT
        [Id],
        [CustomerId],
        [CustomerName],
        [CustomerAlias],
        [CustomerAddress],
        CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END AS [ProductId],
        [ProductBrand],
        CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END AS [ProductName],
        [ProductPackaging],
        [ProductPcsInCtn],
        [BatchNumber],
        [ExpiredDate],
        [InvoiceNo],
        [InvoiceDate],
        [InvoiceQty],
        [InvoiceUnit],
        [UnitPrice],
        [GrossValue],
        [DiscountValue],
        [DiscountPct],
        [NetValue],
        [InKg],
        [GeisaPOId],
        [ShipDate],
        [SalesmanNameGMK],
        [CompanyId],
        [CreatedDate]
    FROM [dbo].[SalesTransaction]
    WHERE [CompanyId] = @CompanyId
      -- Period
      AND (@Year        IS NULL OR YEAR([InvoiceDate])  = @Year)
      AND (@Month       IS NULL OR MONTH([InvoiceDate]) = @Month)
      -- Latest upload batch within the period
      AND [CreatedDate] = (
          SELECT MAX([CreatedDate])
          FROM [dbo].[SalesTransaction]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([InvoiceDate])  = @Year)
            AND (@Month IS NULL OR MONTH([InvoiceDate]) = @Month)
      )
      AND (@SearchTerm IS NULL OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END LIKE @SearchTerm OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END LIKE @SearchTerm)
      -- String filters
      AND (@CustomerId        IS NULL OR [CustomerId]       LIKE @CustomerId)
      AND (@CustomerName      IS NULL OR [CustomerName]     LIKE @CustomerName)
      AND (@CustomerAlias     IS NULL OR [CustomerAlias]    LIKE @CustomerAlias)
      AND (@CustomerAddress   IS NULL OR [CustomerAddress]  LIKE @CustomerAddress)
      AND (@ProductId         IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END LIKE @ProductId)
      AND (@ProductBrand      IS NULL OR [ProductBrand]     LIKE @ProductBrand)
      AND (@ProductName       IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END LIKE @ProductName)
      AND (@ProductPackaging  IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@BatchNumber       IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      AND (@InvoiceNo         IS NULL OR [InvoiceNo]        LIKE @InvoiceNo)
      AND (@InvoiceUnit       IS NULL OR [InvoiceUnit]      LIKE @InvoiceUnit)
      AND (@GeisaPOId         IS NULL OR [GeisaPOId]        LIKE @GeisaPOId)
      AND (@SalesmanNameGMK   IS NULL OR [SalesmanNameGMK]  LIKE @SalesmanNameGMK)
      -- Integer filters
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      -- Decimal filters
      AND (@InvoiceQty    IS NULL OR [InvoiceQty]    = @InvoiceQty)
      AND (@UnitPrice     IS NULL OR [UnitPrice]     = @UnitPrice)
      AND (@GrossValue    IS NULL OR [GrossValue]    = @GrossValue)
      AND (@DiscountValue IS NULL OR [DiscountValue] = @DiscountValue)
      AND (@DiscountPct   IS NULL OR [DiscountPct]   = @DiscountPct)
      AND (@NetValue      IS NULL OR [NetValue]      = @NetValue)
      AND (@InKg          IS NULL OR [InKg]          = @InKg)
      -- DateTime filters (exact date match, ignoring time)
      AND (@ExpiredDate  IS NULL OR CAST([ExpiredDate]  AS DATE) = CAST(@ExpiredDate  AS DATE))
      AND (@InvoiceDate  IS NULL OR CAST([InvoiceDate]  AS DATE) = CAST(@InvoiceDate  AS DATE))
      AND (@ShipDate     IS NULL OR CAST([ShipDate]     AS DATE) = CAST(@ShipDate     AS DATE))
      AND (@CreatedDate  IS NULL OR CAST([CreatedDate]  AS DATE) = CAST(@CreatedDate  AS DATE))
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''invoiceNo'', ''InvoiceNo'')
            THEN [InvoiceNo]       END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''customerId'', ''CustomerId'')
            THEN [CustomerId]      END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''customerName'', ''CustomerName'')
            THEN [CustomerName]    END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''productId'', ''ProductId'')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''productBrand'', ''ProductBrand'')
            THEN [ProductBrand]    END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''productName'', ''ProductName'')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''productPackaging'', ''ProductPackaging'')
            THEN [ProductPackaging] END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''batchNumber'', ''BatchNumber'')
            THEN [BatchNumber]     END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''invoiceUnit'', ''InvoiceUnit'')
            THEN [InvoiceUnit]     END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''geisaPOId'', ''GeisaPOId'')
            THEN [GeisaPOId]       END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''salesmanNameGMK'', ''SalesmanNameGMK'')
            THEN [SalesmanNameGMK] END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''invoiceNo'', ''InvoiceNo'')
            THEN [InvoiceNo]       END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''customerId'', ''CustomerId'')
            THEN [CustomerId]      END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''customerName'', ''CustomerName'')
            THEN [CustomerName]    END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''productId'', ''ProductId'')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''productBrand'', ''ProductBrand'')
            THEN [ProductBrand]    END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''productName'', ''ProductName'')
            THEN CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''productPackaging'', ''ProductPackaging'')
            THEN [ProductPackaging] END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''batchNumber'', ''BatchNumber'')
            THEN [BatchNumber]     END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''invoiceUnit'', ''InvoiceUnit'')
            THEN [InvoiceUnit]     END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''geisaPOId'', ''GeisaPOId'')
            THEN [GeisaPOId]       END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''salesmanNameGMK'', ''SalesmanNameGMK'')
            THEN [SalesmanNameGMK] END DESC,
        -- DateTime columns ASC
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''invoiceDate'', ''InvoiceDate'')
            THEN [InvoiceDate]     END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''expiredDate'', ''ExpiredDate'')
            THEN [ExpiredDate]     END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''shipDate'', ''ShipDate'')
            THEN [ShipDate]        END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''createdDate'', ''CreatedDate'')
            THEN [CreatedDate]     END ASC,
        -- DateTime columns DESC
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''invoiceDate'', ''InvoiceDate'')
            THEN [InvoiceDate]     END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''expiredDate'', ''ExpiredDate'')
            THEN [ExpiredDate]     END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''shipDate'', ''ShipDate'')
            THEN [ShipDate]        END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''createdDate'', ''CreatedDate'')
            THEN [CreatedDate]     END DESC,
        -- Decimal columns ASC
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''invoiceQty'', ''InvoiceQty'')
            THEN [InvoiceQty]      END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''unitPrice'', ''UnitPrice'')
            THEN [UnitPrice]       END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''grossValue'', ''GrossValue'')
            THEN [GrossValue]      END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''discountValue'', ''DiscountValue'')
            THEN [DiscountValue]   END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''discountPct'', ''DiscountPct'')
            THEN [DiscountPct]     END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''netValue'', ''NetValue'')
            THEN [NetValue]        END ASC,
        CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''inKg'', ''InKg'')
            THEN [InKg]            END ASC,
        -- Decimal columns DESC
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''invoiceQty'', ''InvoiceQty'')
            THEN [InvoiceQty]      END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''unitPrice'', ''UnitPrice'')
            THEN [UnitPrice]       END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''grossValue'', ''GrossValue'')
            THEN [GrossValue]      END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''discountValue'', ''DiscountValue'')
            THEN [DiscountValue]   END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''discountPct'', ''DiscountPct'')
            THEN [DiscountPct]     END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''netValue'', ''NetValue'')
            THEN [NetValue]        END DESC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''inKg'', ''InKg'')
            THEN [InKg]            END DESC,
        -- Integer columns ASC/DESC
        CASE WHEN @SortOrder = ''ASC''  AND @SortColumn IN (''productPcsInCtn'', ''ProductPcsInCtn'')
            THEN [ProductPcsInCtn] END ASC,
        CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''productPcsInCtn'', ''ProductPcsInCtn'')
            THEN [ProductPcsInCtn] END DESC,
        -- Default fallback
        [InvoiceDate] DESC,
        [InvoiceNo]   ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- â”€â”€ TOTAL COUNT RESULT SET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    SELECT COUNT(*)
    FROM [dbo].[SalesTransaction]
    WHERE [CompanyId] = @CompanyId
      AND (@Year        IS NULL OR YEAR([InvoiceDate])  = @Year)
      AND (@Month       IS NULL OR MONTH([InvoiceDate]) = @Month)
      AND [CreatedDate] = (
          SELECT MAX([CreatedDate])
          FROM [dbo].[SalesTransaction]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([InvoiceDate])  = @Year)
            AND (@Month IS NULL OR MONTH([InvoiceDate]) = @Month)
      )
      AND (@SearchTerm IS NULL OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END LIKE @SearchTerm OR
           CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END LIKE @SearchTerm)
      AND (@CustomerId        IS NULL OR [CustomerId]       LIKE @CustomerId)
      AND (@CustomerName      IS NULL OR [CustomerName]     LIKE @CustomerName)
      AND (@CustomerAlias     IS NULL OR [CustomerAlias]    LIKE @CustomerAlias)
      AND (@CustomerAddress   IS NULL OR [CustomerAddress]  LIKE @CustomerAddress)
      AND (@ProductId         IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END LIKE @ProductId)
      AND (@ProductBrand      IS NULL OR [ProductBrand]     LIKE @ProductBrand)
      AND (@ProductName       IS NULL OR CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END LIKE @ProductName)
      AND (@ProductPackaging  IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@BatchNumber       IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      AND (@InvoiceNo         IS NULL OR [InvoiceNo]        LIKE @InvoiceNo)
      AND (@InvoiceUnit       IS NULL OR [InvoiceUnit]      LIKE @InvoiceUnit)
      AND (@GeisaPOId         IS NULL OR [GeisaPOId]        LIKE @GeisaPOId)
      AND (@SalesmanNameGMK   IS NULL OR [SalesmanNameGMK]  LIKE @SalesmanNameGMK)
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      AND (@InvoiceQty    IS NULL OR [InvoiceQty]    = @InvoiceQty)
      AND (@UnitPrice     IS NULL OR [UnitPrice]     = @UnitPrice)
      AND (@GrossValue    IS NULL OR [GrossValue]    = @GrossValue)
      AND (@DiscountValue IS NULL OR [DiscountValue] = @DiscountValue)
      AND (@DiscountPct   IS NULL OR [DiscountPct]   = @DiscountPct)
      AND (@NetValue      IS NULL OR [NetValue]      = @NetValue)
      AND (@InKg          IS NULL OR [InKg]          = @InKg)
      AND (@ExpiredDate  IS NULL OR CAST([ExpiredDate]  AS DATE) = CAST(@ExpiredDate  AS DATE))
      AND (@InvoiceDate  IS NULL OR CAST([InvoiceDate]  AS DATE) = CAST(@InvoiceDate  AS DATE))
      AND (@ShipDate     IS NULL OR CAST([ShipDate]     AS DATE) = CAST(@ShipDate     AS DATE))
      AND (@CreatedDate  IS NULL OR CAST([CreatedDate]  AS DATE) = CAST(@CreatedDate  AS DATE));
END';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE [dbo].[GiselSales_SelectByCustPeriod]
    @CompanyId INT,
    @Year INT,
    @Month INT,
    @UseDistributorIdentity BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    SELECT
        [Id],
        [CustomerId],
        [CustomerName],
        [CustomerAlias],
        [CustomerAddress],
        CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''''),[ProductId]) ELSE [ProductId] END AS [ProductId],
        [ProductBrand],
        CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''''),[ProductName]) ELSE [ProductName] END AS [ProductName],
        [ProductPackaging],
        [ProductPcsInCtn],
        [BatchNumber],
        [ExpiredDate],
        [InvoiceNo],
        [InvoiceDate],
        [InvoiceQty],
        [InvoiceUnit],
        [UnitPrice],
        [GrossValue],
        [DiscountValue],
        [DiscountPct],
        [NetValue],
        [InKg],
        [GeisaPOId],
        [ShipDate],
        [SalesmanNameGMK],
        [CompanyId],
        [CreatedDate]
    FROM [dbo].[SalesTransaction]
    WHERE [CompanyId] = @CompanyId
      AND YEAR([InvoiceDate]) = @Year
      AND MONTH([InvoiceDate]) = @Month
      AND [CreatedDate] = (
          SELECT MAX([CreatedDate])
          FROM [dbo].[SalesTransaction]
          WHERE [CompanyId] = @CompanyId
            AND YEAR([InvoiceDate]) = @Year
            AND MONTH([InvoiceDate]) = @Month
      )
    ORDER BY [InvoiceDate], [InvoiceNo], [Id];
END';

    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260811180000_AddCompanyItemIdentityMapping')
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260811180000_AddCompanyItemIdentityMapping', N'10.0.8');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
