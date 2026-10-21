SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Records where an uploaded workbook was archived on disk. Nullable: archiving is optional, and an
-- upload is still accepted when the archive is switched off or the write fails.

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260916150000-AddUploadArchivedFilePath',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the upload archive deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing this column.', 1;

    IF OBJECT_ID(N'dbo.StockUploadBatch', N'U') IS NULL OR OBJECT_ID(N'dbo.SelloutUploadGroup', N'U') IS NULL
        THROW 51004, 'dbo.StockUploadBatch and dbo.SelloutUploadGroup are required.', 1;

    IF COL_LENGTH(N'dbo.StockUploadBatch', N'ArchivedFilePath') IS NULL
        ALTER TABLE dbo.StockUploadBatch ADD ArchivedFilePath nvarchar(500) NULL;

    IF COL_LENGTH(N'dbo.SelloutUploadGroup', N'ArchivedFilePath') IS NULL
        ALTER TABLE dbo.SelloutUploadGroup ADD ArchivedFilePath nvarchar(500) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260916150000_AddUploadArchivedFilePath'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260916150000_AddUploadArchivedFilePath', N'10.0.8');
    END;

    COMMIT TRANSACTION;
    PRINT 'Upload ArchivedFilePath installation completed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
