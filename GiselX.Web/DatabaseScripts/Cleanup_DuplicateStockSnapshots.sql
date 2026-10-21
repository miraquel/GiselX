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
    Publication replaces per AREA: an upload supersedes only the areas it carries, so one company
    can legitimately hold several live batches on one snapshot date, one per area (Medan uploaded in
    the morning, Binjai in the afternoon). Those are NOT duplicates and this script leaves them be.

    It ranks (batch, area) pairs - taken from the Stock rows of live batches, a row without an area
    counting as area 0 - per company, snapshot date and area, newest completion first, exactly as
    vw_GiselStock_Live and the Stock page do. A pair ranked below the newest is a duplicate: the
    Stock rows of that batch AND that area are deleted. A batch is marked Replaced (status 4) only
    when no Stock rows of it remain, so a batch that still carries another area's live rows stays
    live. For a company without areas this reduces to what publication would have done at the time:
    keep the newest batch's rows for the date.

    What it does NOT touch
    ----------------------
    Staging rows, upload batches' audit fields, validation errors, archived files, or any pair that
    is the live one for its company, date and area. Rows with no batch (the old direct-import path)
    are left alone: they carry no snapshot date and cannot be attributed to one.

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

    -- One row per (batch, area) pair that is a duplicate. AreaKey is COALESCE(StockAreaId, 0).
    DECLARE @Superseded TABLE (BatchId BIGINT, AreaKey INT, CompanyId INT, StockDate DATE,
                               PRIMARY KEY (BatchId, AreaKey));

    -- Everything except the newest completed (batch, area) pair for each company, date and area.
    -- Status 2 = Completed, 3 = CompletedWithWarnings in the Stock enum.
    INSERT INTO @Superseded (BatchId, AreaKey, CompanyId, StockDate)
    SELECT  ranked.Id, ranked.AreaKey, ranked.CompanyId, ranked.StockDate
    FROM    (
                SELECT  b.Id, b.CompanyId, b.StockDate,
                        COALESCE(d.StockAreaId,0) AS AreaKey,
                        ROW_NUMBER() OVER (PARTITION BY b.CompanyId, b.StockDate, COALESCE(d.StockAreaId,0)
                                           ORDER BY b.CompletedDate DESC, b.Id DESC) AS LiveRank
                FROM    dbo.StockUploadBatch b
                JOIN    (SELECT DISTINCT s.StockUploadBatchId, s.StockAreaId
                         FROM   dbo.Stock s) d ON d.StockUploadBatchId = b.Id
                WHERE   b.Status IN (2, 3)
            ) ranked
    WHERE   ranked.LiveRank > 1;

    -- Batches left with no Stock rows once the duplicates are gone.
    DECLARE @Emptied TABLE (BatchId BIGINT PRIMARY KEY);

    INSERT INTO @Emptied (BatchId)
    SELECT  DISTINCT x.BatchId
    FROM    @Superseded x
    WHERE   NOT EXISTS (SELECT 1 FROM dbo.Stock s
                        WHERE s.StockUploadBatchId = x.BatchId
                          AND NOT EXISTS (SELECT 1 FROM @Superseded y
                                          WHERE y.BatchId = s.StockUploadBatchId
                                            AND y.AreaKey = COALESCE(s.StockAreaId,0)));

    SELECT  'Snapshot dates affected'  AS Item,
            CAST(COUNT(DISTINCT CAST(CompanyId AS VARCHAR) + '|' + CONVERT(VARCHAR, StockDate)) AS VARCHAR) AS Value
    FROM    @Superseded
    UNION ALL
    SELECT  'Batches to mark Replaced', CAST(COUNT(*) AS VARCHAR) FROM @Emptied
    UNION ALL
    SELECT  'Stock rows to delete',
            CAST((SELECT COUNT(*) FROM dbo.Stock s
                  JOIN @Superseded x ON x.BatchId = s.StockUploadBatchId
                                    AND x.AreaKey = COALESCE(s.StockAreaId,0)) AS VARCHAR)
    UNION ALL
    SELECT  'Stock rows that remain',
            CAST((SELECT COUNT(*) FROM dbo.Stock s
                  WHERE NOT EXISTS (SELECT 1 FROM @Superseded x
                                    WHERE x.BatchId = s.StockUploadBatchId
                                      AND x.AreaKey = COALESCE(s.StockAreaId,0)))
                 AS VARCHAR);

    -- Per-date and area detail, so the numbers above can be checked against something concrete.
    SELECT  x.CompanyId, c.Name AS CompanyName, x.StockDate,
            a.Name AS AreaName,
            COUNT(DISTINCT x.BatchId) AS BatchesSuperseded,
            COUNT(s.Id)               AS RowsDeleted
    FROM    @Superseded x
    JOIN    dbo.Company c  ON c.Id = x.CompanyId
    LEFT JOIN dbo.StockArea a ON a.Id = NULLIF(x.AreaKey, 0)
    LEFT JOIN dbo.Stock s  ON s.StockUploadBatchId = x.BatchId
                          AND COALESCE(s.StockAreaId,0) = x.AreaKey
    GROUP BY x.CompanyId, c.Name, x.StockDate, a.Name
    ORDER BY c.Name, x.StockDate, a.Name;

    IF @WhatIf = 1
    BEGIN
        PRINT 'PREVIEW ONLY - nothing was changed. Set @WhatIf = 0 to apply.';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    DELETE  stock
    FROM    dbo.Stock stock
    JOIN    @Superseded x ON x.BatchId = stock.StockUploadBatchId
                         AND x.AreaKey = COALESCE(stock.StockAreaId,0);

    -- Status 4 is Replaced in the Stock enum (3 is CompletedWithWarnings there). Only a batch with
    -- no rows left is Replaced; one that still holds another area's live rows stays live.
    UPDATE  b SET Status = 4
    FROM    dbo.StockUploadBatch b
    JOIN    @Emptied e ON e.BatchId = b.Id
    WHERE   NOT EXISTS (SELECT 1 FROM dbo.Stock s WHERE s.StockUploadBatchId = b.Id);

    COMMIT TRANSACTION;
    PRINT 'Duplicate Stock snapshot rows removed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
