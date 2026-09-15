SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260915090000-AddBusinessUnitNotificationEmails',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the BusinessUnit NotificationEmails deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing BusinessUnit NotificationEmails.', 1;

    IF OBJECT_ID(N'dbo.BusinessUnit', N'U') IS NULL
        THROW 51004, 'dbo.BusinessUnit is required before installing BusinessUnit NotificationEmails.', 1;

    IF COL_LENGTH(N'dbo.BusinessUnit', N'NotificationEmails') IS NULL
        ALTER TABLE dbo.BusinessUnit ADD NotificationEmails nvarchar(1000) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.BusinessUnit', N'U')
          AND c.name = N'NotificationEmails'
          AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000
               OR c.is_nullable <> 1)
    )
        ALTER TABLE dbo.BusinessUnit ALTER COLUMN NotificationEmails nvarchar(1000) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260915090000_AddBusinessUnitNotificationEmails'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260915090000_AddBusinessUnitNotificationEmails', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'BusinessUnit NotificationEmails installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
