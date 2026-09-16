/*
    Removes Stock rows left behind by snapshot dates that were uploaded more than once BEFORE
    publication started superseding.

    THIS DELETES DATA. Read the preview before running it for real.

    Background
    ----------
    Stock publication used to only INSERT. Re-uploading a snapshot date left the earlier batch's
    rows in dbo.Stock alongside the new ones, so every product for that date appeared twice and any
    total was doubled. Publication now supersedes — it deletes the previous batch's rows and marks
    that batch Replaced (status 4) — so no NEW duplicates can appear. Rows created before the fix
    are still there.

    What it does
    ------------
    For each company and snapshot date with more than one completed batch, it keeps the most
    recently completed batch and deletes the Stock rows of the earlier ones, marking those batches
    Replaced. It is exactly what publication would have done at the time.

    What it does NOT touch
    ----------------------
    Staging rows, upload batches' audit fields, validation errors, archived files, or any batch that
    is the live one for its date. Rows with no batch (the old direct-import path) are left alone:
    they carry no snapshot date and cannot be attributed to one.

    How to run it
    -------------
    1. Run as-is. @WhatIf is 1, so it only reports what would be deleted and changes nothing.
    2. Read the numbers. If they look right, set @WhatIf = 0 and run again.
    3. Take a database backup first. This cannot be undone.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @WhatIf BIT = 1;   -- <<< set to 0 to actually delete

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Superseded TABLE (BatchId BIGINT PRIMARY KEY, CompanyId INT, StockDate DATE);

    -- Everything except the newest completed batch for each company and snapshot date.
    INSERT INTO @Superseded (BatchId, CompanyId, StockDate)
    SELECT  ranked.Id, ranked.CompanyId, ranked.StockDate
    FROM    (
                SELECT  b.Id, b.CompanyId, b.StockDate,
                        ROW_NUMBER() OVER (PARTITION BY b.CompanyId, b.StockDate
                                           ORDER BY b.CompletedDate DESC, b.Id DESC) AS LiveRank
                FROM    dbo.StockUploadBatch b
                WHERE   b.Status IN (2, 3)          -- Completed, CompletedWithWarnings
            ) ranked
    WHERE   ranked.LiveRank > 1;

    SELECT  'Snapshot dates affected'  AS Item,
            CAST(COUNT(DISTINCT CAST(CompanyId AS VARCHAR) + '|' + CONVERT(VARCHAR, StockDate)) AS VARCHAR) AS Value
    FROM    @Superseded
    UNION ALL
    SELECT  'Batches to mark Replaced', CAST(COUNT(*) AS VARCHAR) FROM @Superseded
    UNION ALL
    SELECT  'Stock rows to delete',
            CAST((SELECT COUNT(*) FROM dbo.Stock s
                  JOIN @Superseded x ON x.BatchId = s.StockUploadBatchId) AS VARCHAR)
    UNION ALL
    SELECT  'Stock rows that remain',
            CAST((SELECT COUNT(*) FROM dbo.Stock s
                  WHERE NOT EXISTS (SELECT 1 FROM @Superseded x WHERE x.BatchId = s.StockUploadBatchId))
                 AS VARCHAR);

    -- Per-date detail, so the numbers above can be checked against something concrete.
    SELECT  x.CompanyId, c.Name AS CompanyName, x.StockDate,
            COUNT(DISTINCT x.BatchId) AS BatchesSuperseded,
            COUNT(s.Id)               AS RowsDeleted
    FROM    @Superseded x
    JOIN    dbo.Company c  ON c.Id = x.CompanyId
    LEFT JOIN dbo.Stock s  ON s.StockUploadBatchId = x.BatchId
    GROUP BY x.CompanyId, c.Name, x.StockDate
    ORDER BY c.Name, x.StockDate;

    IF @WhatIf = 1
    BEGIN
        PRINT 'PREVIEW ONLY - nothing was changed. Set @WhatIf = 0 to apply.';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    DELETE  stock
    FROM    dbo.Stock stock
    JOIN    @Superseded x ON x.BatchId = stock.StockUploadBatchId;

    -- Status 4 is Replaced in the Stock enum (3 is CompletedWithWarnings there).
    UPDATE  b SET Status = 4
    FROM    dbo.StockUploadBatch b
    JOIN    @Superseded x ON x.BatchId = b.Id;

    COMMIT TRANSACTION;
    PRINT 'Duplicate Stock snapshot rows removed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
