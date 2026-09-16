SET NOCOUNT ON;
SET XACT_ABORT ON;

-- SMTP configuration moves out of appsettings.json, which a copy-paste deployment overwrites, and
-- into a single-row table an operator can edit from the Email Settings screen.

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260916090000-AddEmailSetting',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the EmailSetting deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing EmailSetting.', 1;

    IF OBJECT_ID(N'dbo.EmailSetting', N'U') IS NULL
    CREATE TABLE dbo.EmailSetting
    (
        Id                int            NOT NULL CONSTRAINT PK_EmailSetting PRIMARY KEY,
        SmtpHost          nvarchar(255)  NOT NULL,
        SmtpPort          int            NOT NULL,
        EnableSsl         bit            NOT NULL,
        Username          nvarchar(255)  NULL,
        PasswordProtected nvarchar(2000) NULL,
        FromAddress       nvarchar(255)  NOT NULL,
        FromName          nvarchar(255)  NOT NULL,
        DashboardBaseUrl  nvarchar(500)  NULL,
        SupportEmail      nvarchar(255)  NULL,
        SupportPhone      nvarchar(100)  NULL,
        TimeZoneId        nvarchar(100)  NOT NULL,
        ModifiedDate      datetime2      NULL,
        ModifiedBy        nvarchar(256)  NULL,
        CONSTRAINT CK_EmailSetting_Singleton CHECK (Id = 1)
    );

    -- "identity" is the T-SQL IDENTITY keyword, so the schema must be bracketed or this will not parse.
    INSERT INTO [identity].[AspNetRoleClaims] (RoleId, ClaimType, ClaimValue)
    SELECT existing.RoleId, Npermission, NEmailSettings.Manage
    FROM [identity].[AspNetRoleClaims] existing
    WHERE existing.ClaimType = Npermission
      AND existing.ClaimValue = NEmailTest.Send
      AND NOT EXISTS
      (
          SELECT 1 FROM [identity].[AspNetRoleClaims] already
          WHERE already.RoleId = existing.RoleId
            AND already.ClaimType = Npermission
            AND already.ClaimValue = NEmailSettings.Manage
      );

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260916090000_AddEmailSetting'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260916090000_AddEmailSetting', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'EmailSetting installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
