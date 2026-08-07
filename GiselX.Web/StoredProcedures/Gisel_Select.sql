CREATE OR ALTER PROCEDURE [dbo].[Gisel_Select]
    -- Identity & paging
    @CreatedBy      NVARCHAR(256),
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- Period filter (from year/month dropdowns, based on SoCreateDate)
    @Year           INT            = NULL,
    @Month          INT            = NULL,
    -- String column filters (support LIKE via %)
    @SoId           NVARCHAR(100)  = NULL,
    @ItemId         NVARCHAR(100)  = NULL,
    @ItemName       NVARCHAR(200)  = NULL,
    @Unit           NVARCHAR(50)   = NULL,
    -- Integer column filters
    @LeadTimeDlv    INT            = NULL,
    @LeadTimeRct    INT            = NULL,
    -- Decimal column filters
    @SoQty          DECIMAL(18, 2) = NULL,
    @KgPerUnit      DECIMAL(18, 2) = NULL,
    @DoQty          DECIMAL(18, 2) = NULL,
    -- DateTime column filters
    @SoCreateDate   DATETIME       = NULL,
    @DlvDateRequest DATETIME       = NULL,
    @RctDateRequest DATETIME       = NULL,
    @DoDate         DATETIME       = NULL,
    @ReceiptDate    DATETIME       = NULL,
    @CreatedDate    DATETIME       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- ── DATA RESULT SET ─────────────────────────────────────────────────────────
    SELECT
        [Id],
        [SoId],
        [SoCreateDate],
        [LeadTimeDlv],
        [LeadTimeRct],
        [ItemId],
        [ItemName],
        [SoQty],
        [Unit],
        [KgPerUnit],
        [DlvDateRequest],
        [RctDateRequest],
        [DoQty],
        [DoDate],
        [ReceiptDate],
        [CreatedBy],
        [CreatedDate],
        [CompanyId]
    FROM [dbo].[TransDist]
    WHERE [CreatedBy] = @CreatedBy
      -- Period
      AND (@Year  IS NULL OR YEAR([SoCreateDate])  = @Year)
      AND (@Month IS NULL OR MONTH([SoCreateDate]) = @Month)
      -- String filters
      AND (@SoId     IS NULL OR [SoId]     LIKE @SoId)
      AND (@ItemId   IS NULL OR [ItemId]   LIKE @ItemId)
      AND (@ItemName IS NULL OR [ItemName] LIKE @ItemName)
      AND (@Unit     IS NULL OR [Unit]     LIKE @Unit)
      -- Integer filters
      AND (@LeadTimeDlv IS NULL OR [LeadTimeDlv] = @LeadTimeDlv)
      AND (@LeadTimeRct IS NULL OR [LeadTimeRct] = @LeadTimeRct)
      -- Decimal filters
      AND (@SoQty    IS NULL OR [SoQty]    = @SoQty)
      AND (@KgPerUnit IS NULL OR [KgPerUnit] = @KgPerUnit)
      AND (@DoQty    IS NULL OR [DoQty]    = @DoQty)
      -- DateTime filters (exact date match, ignoring time)
      AND (@SoCreateDate   IS NULL OR CAST([SoCreateDate]   AS DATE) = CAST(@SoCreateDate   AS DATE))
      AND (@DlvDateRequest IS NULL OR CAST([DlvDateRequest] AS DATE) = CAST(@DlvDateRequest AS DATE))
      AND (@RctDateRequest IS NULL OR CAST([RctDateRequest] AS DATE) = CAST(@RctDateRequest AS DATE))
      AND (@DoDate         IS NULL OR CAST([DoDate]         AS DATE) = CAST(@DoDate         AS DATE))
      AND (@ReceiptDate    IS NULL OR CAST([ReceiptDate]    AS DATE) = CAST(@ReceiptDate    AS DATE))
      AND (@CreatedDate    IS NULL OR CAST([CreatedDate]    AS DATE) = CAST(@CreatedDate    AS DATE))
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('soId', 'SoId')
            THEN [SoId]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('itemId', 'ItemId')
            THEN [ItemId]   END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('itemName', 'ItemName')
            THEN [ItemName] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('unit', 'Unit')
            THEN [Unit]     END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('soId', 'SoId')
            THEN [SoId]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('itemId', 'ItemId')
            THEN [ItemId]   END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('itemName', 'ItemName')
            THEN [ItemName] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('unit', 'Unit')
            THEN [Unit]     END DESC,
        -- DateTime columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('soCreateDate', 'SoCreateDate')
            THEN [SoCreateDate]   END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('dlvDateRequest', 'DlvDateRequest')
            THEN [DlvDateRequest] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('rctDateRequest', 'RctDateRequest')
            THEN [RctDateRequest] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('doDate', 'DoDate')
            THEN [DoDate]         END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('receiptDate', 'ReceiptDate')
            THEN [ReceiptDate]    END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]    END ASC,
        -- DateTime columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('soCreateDate', 'SoCreateDate')
            THEN [SoCreateDate]   END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('dlvDateRequest', 'DlvDateRequest')
            THEN [DlvDateRequest] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('rctDateRequest', 'RctDateRequest')
            THEN [RctDateRequest] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('doDate', 'DoDate')
            THEN [DoDate]         END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('receiptDate', 'ReceiptDate')
            THEN [ReceiptDate]    END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]    END DESC,
        -- Decimal columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('soQty', 'SoQty')
            THEN [SoQty]     END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('kgPerUnit', 'KgPerUnit')
            THEN [KgPerUnit] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('doQty', 'DoQty')
            THEN [DoQty]     END ASC,
        -- Decimal columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('soQty', 'SoQty')
            THEN [SoQty]     END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('kgPerUnit', 'KgPerUnit')
            THEN [KgPerUnit] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('doQty', 'DoQty')
            THEN [DoQty]     END DESC,
        -- Integer columns ASC/DESC
        CASE WHEN @SortOrder = 'ASC'  AND @SortColumn IN ('leadTimeDlv', 'LeadTimeDlv')
            THEN [LeadTimeDlv] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('leadTimeDlv', 'LeadTimeDlv')
            THEN [LeadTimeDlv] END DESC,
        CASE WHEN @SortOrder = 'ASC'  AND @SortColumn IN ('leadTimeRct', 'LeadTimeRct')
            THEN [LeadTimeRct] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('leadTimeRct', 'LeadTimeRct')
            THEN [LeadTimeRct] END DESC,
        -- Default fallback
        [SoCreateDate] DESC,
        [SoId]         ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- ── TOTAL COUNT RESULT SET ───────────────────────────────────────────────────
    SELECT COUNT(*)
    FROM [dbo].[TransDist]
    WHERE [CreatedBy] = @CreatedBy
      AND (@Year  IS NULL OR YEAR([SoCreateDate])  = @Year)
      AND (@Month IS NULL OR MONTH([SoCreateDate]) = @Month)
      AND (@SoId     IS NULL OR [SoId]     LIKE @SoId)
      AND (@ItemId   IS NULL OR [ItemId]   LIKE @ItemId)
      AND (@ItemName IS NULL OR [ItemName] LIKE @ItemName)
      AND (@Unit     IS NULL OR [Unit]     LIKE @Unit)
      AND (@LeadTimeDlv IS NULL OR [LeadTimeDlv] = @LeadTimeDlv)
      AND (@LeadTimeRct IS NULL OR [LeadTimeRct] = @LeadTimeRct)
      AND (@SoQty    IS NULL OR [SoQty]    = @SoQty)
      AND (@KgPerUnit IS NULL OR [KgPerUnit] = @KgPerUnit)
      AND (@DoQty    IS NULL OR [DoQty]    = @DoQty)
      AND (@SoCreateDate   IS NULL OR CAST([SoCreateDate]   AS DATE) = CAST(@SoCreateDate   AS DATE))
      AND (@DlvDateRequest IS NULL OR CAST([DlvDateRequest] AS DATE) = CAST(@DlvDateRequest AS DATE))
      AND (@RctDateRequest IS NULL OR CAST([RctDateRequest] AS DATE) = CAST(@RctDateRequest AS DATE))
      AND (@DoDate         IS NULL OR CAST([DoDate]         AS DATE) = CAST(@DoDate         AS DATE))
      AND (@ReceiptDate    IS NULL OR CAST([ReceiptDate]    AS DATE) = CAST(@ReceiptDate    AS DATE))
      AND (@CreatedDate    IS NULL OR CAST([CreatedDate]    AS DATE) = CAST(@CreatedDate    AS DATE));
END
