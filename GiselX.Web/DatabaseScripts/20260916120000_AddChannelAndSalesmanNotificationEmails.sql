SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Internal notification recipients can now also come from the Channel and Salesman masters, not
-- only the Business Unit. Both gain the same ';'-delimited list.

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260916120000-AddChannelAndSalesmanNotificationEmails',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Channel/Salesman NotificationEmails deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing these columns.', 1;

    IF OBJECT_ID(N'dbo.Channel', N'U') IS NULL OR OBJECT_ID(N'dbo.Salesman', N'U') IS NULL
        THROW 51004, 'Run 20260904030000_CreateChannelAndSalesmanMaster first.', 1;

    -- ---- dbo.Channel ----
    IF COL_LENGTH(N'dbo.Channel', N'NotificationEmails') IS NULL
        ALTER TABLE dbo.Channel ADD NotificationEmails nvarchar(1000) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Channel', N'U')
          AND c.name = N'NotificationEmails'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000
               OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Channel ALTER COLUMN NotificationEmails nvarchar(1000) NULL;

    -- ---- dbo.Salesman ----
    IF COL_LENGTH(N'dbo.Salesman', N'NotificationEmails') IS NULL
        ALTER TABLE dbo.Salesman ADD NotificationEmails nvarchar(1000) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Salesman', N'U')
          AND c.name = N'NotificationEmails'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000
               OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.Salesman ALTER COLUMN NotificationEmails nvarchar(1000) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260916120000_AddChannelAndSalesmanNotificationEmails'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260916120000_AddChannelAndSalesmanNotificationEmails', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Channel and Salesman NotificationEmails installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
