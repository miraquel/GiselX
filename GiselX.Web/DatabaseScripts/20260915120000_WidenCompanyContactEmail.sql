SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ContactEmail now holds a ';'-delimited list of addresses. The column is widened AND the three
-- Company procedures that carry the value are reinstalled together: @ContactEmail was declared
-- NVARCHAR(255), so widening the column alone would silently truncate a longer list at the
-- procedure boundary. The procedure bodies match what 20260904030000_CreateChannelAndSalesmanMaster
-- deployed, including @ChannelId and @SalesmanId.

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260915120000-WidenCompanyContactEmail',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Company ContactEmail deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before widening Company ContactEmail.', 1;

    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
        THROW 51004, 'dbo.Company is required before widening Company ContactEmail.', 1;

    IF COL_LENGTH(N'dbo.Company', N'ChannelId') IS NULL OR COL_LENGTH(N'dbo.Company', N'SalesmanId') IS NULL
        THROW 51005, 'Run 20260904030000_CreateChannelAndSalesmanMaster first: these procedures reference ChannelId and SalesmanId.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Company', N'U')
          AND c.name = N'ContactEmail'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000)
    )
        ALTER TABLE dbo.Company ALTER COLUMN ContactEmail nvarchar(1000) NULL;

    -- ---- dbo.Company_Insert ----
    EXEC(N'CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @ContactEmail NVARCHAR(1000) = NULL, @DeadlineDayOfMonth INT = NULL,
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
END
');

    -- ---- dbo.Company_Update ----
    EXEC(N'CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id INT, @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @ContactEmail NVARCHAR(1000) = NULL, @DeadlineDayOfMonth INT = NULL,
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
END
');

    -- ---- dbo.Company_Select ----
    EXEC(N'CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber INT = 1, @PageSize INT = 10, @SortColumn NVARCHAR(100) = NULL, @SortOrder NVARCHAR(4) = ''ASC'',
    @Name NVARCHAR(100) = NULL, @BusinessUnitName NVARCHAR(100) = NULL, @ChannelName NVARCHAR(200) = NULL,
    @SalesmanName NVARCHAR(200) = NULL, @Address NVARCHAR(255) = NULL,
    @ContactEmail NVARCHAR(1000) = NULL, @Id INT = NULL, @DeadlineDayOfMonth INT = NULL,
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
END
');

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260915120000_WidenCompanyContactEmail'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260915120000_WidenCompanyContactEmail', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Company ContactEmail widening completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
