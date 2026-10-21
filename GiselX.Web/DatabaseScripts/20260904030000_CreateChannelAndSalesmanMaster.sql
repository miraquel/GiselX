SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260904030000-CreateChannelAndSalesmanMaster',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51200, 'Could not acquire the Channel and Salesman deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51201, 'dbo.__EFMigrationsHistory is required before installing Channel and Salesman.', 1;

    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
        THROW 51202, 'dbo.Company is required before installing Channel and Salesman.', 1;

    IF OBJECT_ID(N'dbo.BusinessUnit', N'U') IS NULL
        THROW 51203, 'dbo.BusinessUnit is required before installing Channel and Salesman.', 1;

    IF OBJECT_ID(N'dbo.Channel', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Channel
        (
            Id int IDENTITY(1,1) NOT NULL,
            Code nvarchar(50) NOT NULL,
            Name nvarchar(200) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_Channel_IsActive DEFAULT (1),
            CreatedDate datetime2 NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            ModifiedDate datetime2 NULL,
            ModifiedBy nvarchar(256) NULL,
            CONSTRAINT PK_Channel PRIMARY KEY (Id)
        );
    END;

    IF OBJECT_ID(N'dbo.Salesman', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Salesman
        (
            Id int IDENTITY(1,1) NOT NULL,
            Code nvarchar(50) NOT NULL,
            Name nvarchar(200) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_Salesman_IsActive DEFAULT (1),
            CreatedDate datetime2 NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            ModifiedDate datetime2 NULL,
            ModifiedBy nvarchar(256) NULL,
            CONSTRAINT PK_Salesman PRIMARY KEY (Id)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_Channel_Code' AND object_id = OBJECT_ID(N'dbo.Channel', N'U')
    )
        CREATE UNIQUE INDEX UX_Channel_Code ON dbo.Channel (Code);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_Salesman_Code' AND object_id = OBJECT_ID(N'dbo.Salesman', N'U')
    )
        CREATE UNIQUE INDEX UX_Salesman_Code ON dbo.Salesman (Code);

    IF COL_LENGTH(N'dbo.Company', N'ChannelId') IS NULL
        ALTER TABLE dbo.Company ADD ChannelId int NULL;

    IF COL_LENGTH(N'dbo.Company', N'SalesmanId') IS NULL
        ALTER TABLE dbo.Company ADD SalesmanId int NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_Company_ChannelId' AND object_id = OBJECT_ID(N'dbo.Company', N'U')
    )
        CREATE INDEX IX_Company_ChannelId ON dbo.Company (ChannelId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_Company_SalesmanId' AND object_id = OBJECT_ID(N'dbo.Company', N'U')
    )
        CREATE INDEX IX_Company_SalesmanId ON dbo.Company (SalesmanId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_Company_Channel_ChannelId'
          AND parent_object_id = OBJECT_ID(N'dbo.Company', N'U')
    )
        ALTER TABLE dbo.Company
            ADD CONSTRAINT FK_Company_Channel_ChannelId
            FOREIGN KEY (ChannelId) REFERENCES dbo.Channel (Id);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_Company_Salesman_SalesmanId'
          AND parent_object_id = OBJECT_ID(N'dbo.Company', N'U')
    )
        ALTER TABLE dbo.Company
            ADD CONSTRAINT FK_Company_Salesman_SalesmanId
            FOREIGN KEY (SalesmanId) REFERENCES dbo.Salesman (Id);

    EXEC(N'
CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @ContactEmail NVARCHAR(255) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Company]
        (Name, BusinessUnitId, ChannelId, SalesmanId, Address, ContactEmail, DeadlineDayOfMonth,
         DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES
        (@Name, @BusinessUnitId, @ChannelId, @SalesmanId, @Address, @ContactEmail, @DeadlineDayOfMonth,
         @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = SCOPE_IDENTITY();
END');

    EXEC(N'
CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id INT, @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @ContactEmail NVARCHAR(255) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Company]
    SET Name = @Name, BusinessUnitId = @BusinessUnitId, ChannelId = @ChannelId, SalesmanId = @SalesmanId,
        Address = @Address, ContactEmail = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth, DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays = @ReminderLeadDays, ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = @Id;
END');

    EXEC(N'
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById] @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = @Id;
END');

    EXEC(N'
CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber INT = 1, @PageSize INT = 10, @SortColumn NVARCHAR(100) = NULL, @SortOrder NVARCHAR(4) = ''ASC'',
    @Name NVARCHAR(100) = NULL, @BusinessUnitName NVARCHAR(100) = NULL, @ChannelName NVARCHAR(200) = NULL,
    @SalesmanName NVARCHAR(200) = NULL, @Address NVARCHAR(255) = NULL,
    @ContactEmail NVARCHAR(255) = NULL, @Id INT = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = NULL, @ItemMappingEnabled BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@ChannelName IS NULL OR channel.Name LIKE @ChannelName)
      AND (@SalesmanName IS NULL OR salesman.Name LIKE @SalesmanName)
      AND (@Address IS NULL OR company.Address LIKE @Address)
      AND (@ContactEmail IS NULL OR company.ContactEmail LIKE @ContactEmail)
      AND (@Id IS NULL OR company.Id = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.DeadlineDayOfMonth = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.DeadlineDaysOfWeek = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR company.ReminderLeadDays = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.ItemMappingEnabled = @ItemMappingEnabled)
    ORDER BY
      CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''name'',''Name'') THEN company.Name END ASC,
      CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''businessUnitName'',''BusinessUnitName'') THEN unit.Name END ASC,
      CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''channelName'',''ChannelName'') THEN channel.Name END ASC,
      CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''salesmanName'',''SalesmanName'') THEN salesman.Name END ASC,
      CASE WHEN @SortOrder = ''ASC'' AND @SortColumn IN (''address'',''Address'') THEN company.Address END ASC,
      CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''name'',''Name'') THEN company.Name END DESC,
      CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''businessUnitName'',''BusinessUnitName'') THEN unit.Name END DESC,
      CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''channelName'',''ChannelName'') THEN channel.Name END DESC,
      CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''salesmanName'',''SalesmanName'') THEN salesman.Name END DESC,
      CASE WHEN @SortOrder = ''DESC'' AND @SortColumn IN (''address'',''Address'') THEN company.Address END DESC,
      company.Name ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    SELECT COUNT(*)
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@ChannelName IS NULL OR channel.Name LIKE @ChannelName)
      AND (@SalesmanName IS NULL OR salesman.Name LIKE @SalesmanName)
      AND (@Address IS NULL OR company.Address LIKE @Address)
      AND (@ContactEmail IS NULL OR company.ContactEmail LIKE @ContactEmail)
      AND (@Id IS NULL OR company.Id = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.DeadlineDayOfMonth = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.DeadlineDaysOfWeek = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR company.ReminderLeadDays = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.ItemMappingEnabled = @ItemMappingEnabled);
END');

    EXEC(N'
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth,
           company.DeadlineDaysOfWeek, company.ReminderLeadDays
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.ContactEmail IS NOT NULL
      AND (company.DeadlineDayOfMonth IS NOT NULL OR company.DeadlineDaysOfWeek IS NOT NULL);
END');

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260904030000_CreateChannelAndSalesmanMaster'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260904030000_CreateChannelAndSalesmanMaster', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Channel and Salesman master installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
