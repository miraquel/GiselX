-- Grants the mapping and master-data permissions to every role that already holds Companies.Edit.
--
-- Those screens used to be gated on Companies.Edit and now carry their own permissions. Without this
-- backfill they go dark on an existing database: the startup seeder only grants permissions while the
-- user table is still empty, which is never true in production.
--
-- Adds claims only. It never removes a claim and never touches a role, a user, or business data.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260911165830-GrantMappingPermissionsToCompanyEditors',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the mapping permission grant deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before granting mapping permissions.', 1;

    IF OBJECT_ID(N'[identity].[AspNetRoleClaims]', N'U') IS NULL
        THROW 51004, '[identity].[AspNetRoleClaims] is required before granting mapping permissions.', 1;

    WITH Granted(Value) AS
    (
        SELECT Value FROM
        (
            VALUES
                (N'StockTemplateMappings.Index'), (N'StockTemplateMappings.Manage'), (N'StockTemplateMappings.Delete'),
                (N'DistributorTemplateMappings.Index'), (N'DistributorTemplateMappings.Manage'), (N'DistributorTemplateMappings.Delete'),
                (N'ItemMappings.Index'), (N'ItemMappings.Manage'), (N'ItemMappings.Delete'), (N'ItemMappings.Import'),
                (N'BusinessUnits.Index'), (N'BusinessUnits.Manage'), (N'BusinessUnits.Delete'),
                (N'Channels.Index'), (N'Channels.Manage'), (N'Channels.Delete'),
                (N'Salesmen.Index'), (N'Salesmen.Manage'), (N'Salesmen.Delete')
        ) AS v(Value)
    ),
    Editors(RoleId) AS
    (
        SELECT DISTINCT RoleId FROM [identity].[AspNetRoleClaims]
        WHERE ClaimType = N'permission' AND ClaimValue = N'Companies.Edit'
    )
    INSERT INTO [identity].[AspNetRoleClaims] (RoleId, ClaimType, ClaimValue)
    SELECT e.RoleId, N'permission', g.Value
    FROM Editors e
    CROSS JOIN Granted g
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [identity].[AspNetRoleClaims] existing
        WHERE existing.RoleId = e.RoleId
          AND existing.ClaimType = N'permission'
          AND existing.ClaimValue = g.Value
    );

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260911165830_GrantMappingPermissionsToCompanyEditors'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260911165830_GrantMappingPermissionsToCompanyEditors', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Mapping permission grant completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
