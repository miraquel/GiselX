CREATE OR ALTER PROCEDURE [dbo].[GiselStock_Select]
    -- Paging & sorting
    @CompanyId      INT,
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- Period filter (from year/month dropdowns, based on CreatedDate)
    @Year           INT            = NULL,
    @Month          INT            = NULL,
    -- String column filters (support LIKE via %)
    @ProductId          NVARCHAR(100)  = NULL,
    @ProductName        NVARCHAR(200)  = NULL,
    @ProductPackaging   NVARCHAR(100)  = NULL,
    @ProductUnit        NVARCHAR(50)   = NULL,
    @BatchNumber        NVARCHAR(100)  = NULL,
    -- Integer column filters
    @ProductPcsInCtn    INT            = NULL,
    -- Decimal column filters
    @ProductNetto   DECIMAL(18, 2) = NULL,
    @SaldoAwal      DECIMAL(18, 2) = NULL,
    @SaldoMasukPO   DECIMAL(18, 2) = NULL,
    @SaldoAkhir     DECIMAL(18, 2) = NULL,
    -- DateTime column filters
    @ExpiredDate    DATETIME       = NULL,
    @CreatedDate    DATETIME       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- ── DATA RESULT SET ─────────────────────────────────────────────────────────
    SELECT
        [Id],
        [ProductId],
        [ProductName],
        [ProductPackaging],
        [ProductPcsInCtn],
        [ProductNetto],
        [ProductUnit],
        [SaldoAwal],
        [SaldoMasukPO],
        [SaldoAkhir],
        [BatchNumber],
        [ExpiredDate],
        [CompanyId],
        [CreatedDate]
    FROM [dbo].[Stock]
    WHERE [CompanyId] = @CompanyId
      -- Period
      AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
      AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      -- Latest upload batch within the period
      AND CAST([CreatedDate] AS DATE) = (
          SELECT MAX(CAST([CreatedDate] AS DATE))
          FROM [dbo].[Stock]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
            AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      )
      -- String filters
      AND (@ProductId        IS NULL OR [ProductId]        LIKE @ProductId)
      AND (@ProductName      IS NULL OR [ProductName]      LIKE @ProductName)
      AND (@ProductPackaging IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@ProductUnit      IS NULL OR [ProductUnit]      LIKE @ProductUnit)
      AND (@BatchNumber      IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      -- Integer filters
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      -- Decimal filters
      AND (@ProductNetto IS NULL OR [ProductNetto] = @ProductNetto)
      AND (@SaldoAwal    IS NULL OR [SaldoAwal]    = @SaldoAwal)
      AND (@SaldoMasukPO IS NULL OR [SaldoMasukPO] = @SaldoMasukPO)
      AND (@SaldoAkhir   IS NULL OR [SaldoAkhir]   = @SaldoAkhir)
      -- DateTime filters (exact date match, ignoring time)
      AND (@ExpiredDate IS NULL OR CAST([ExpiredDate] AS DATE) = CAST(@ExpiredDate AS DATE))
      AND (@CreatedDate IS NULL OR CAST([CreatedDate] AS DATE) = CAST(@CreatedDate AS DATE))
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productId', 'ProductId')
            THEN [ProductId]        END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productName', 'ProductName')
            THEN [ProductName]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productPackaging', 'ProductPackaging')
            THEN [ProductPackaging] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productUnit', 'ProductUnit')
            THEN [ProductUnit]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('batchNumber', 'BatchNumber')
            THEN [BatchNumber]      END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productId', 'ProductId')
            THEN [ProductId]        END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productName', 'ProductName')
            THEN [ProductName]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productPackaging', 'ProductPackaging')
            THEN [ProductPackaging] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productUnit', 'ProductUnit')
            THEN [ProductUnit]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('batchNumber', 'BatchNumber')
            THEN [BatchNumber]      END DESC,
        -- DateTime columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('expiredDate', 'ExpiredDate')
            THEN [ExpiredDate]  END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]  END ASC,
        -- DateTime columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('expiredDate', 'ExpiredDate')
            THEN [ExpiredDate]  END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('createdDate', 'CreatedDate')
            THEN [CreatedDate]  END DESC,
        -- Decimal columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('productNetto', 'ProductNetto')
            THEN [ProductNetto] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('saldoAwal', 'SaldoAwal')
            THEN [SaldoAwal]    END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('saldoMasukPO', 'SaldoMasukPO')
            THEN [SaldoMasukPO] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('saldoAkhir', 'SaldoAkhir')
            THEN [SaldoAkhir]   END ASC,
        -- Decimal columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productNetto', 'ProductNetto')
            THEN [ProductNetto] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('saldoAwal', 'SaldoAwal')
            THEN [SaldoAwal]    END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('saldoMasukPO', 'SaldoMasukPO')
            THEN [SaldoMasukPO] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('saldoAkhir', 'SaldoAkhir')
            THEN [SaldoAkhir]   END DESC,
        -- Integer columns ASC/DESC
        CASE WHEN @SortOrder = 'ASC'  AND @SortColumn IN ('productPcsInCtn', 'ProductPcsInCtn')
            THEN [ProductPcsInCtn] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('productPcsInCtn', 'ProductPcsInCtn')
            THEN [ProductPcsInCtn] END DESC,
        -- Default fallback
        [ProductId] ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- ── TOTAL COUNT RESULT SET ───────────────────────────────────────────────────
    SELECT COUNT(*)
    FROM [dbo].[Stock]
    WHERE [CompanyId] = @CompanyId
      AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
      AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      AND CAST([CreatedDate] AS DATE) = (
          SELECT MAX(CAST([CreatedDate] AS DATE))
          FROM [dbo].[Stock]
          WHERE [CompanyId] = @CompanyId
            AND (@Year  IS NULL OR YEAR([CreatedDate])  = @Year)
            AND (@Month IS NULL OR MONTH([CreatedDate]) = @Month)
      )
      AND (@ProductId        IS NULL OR [ProductId]        LIKE @ProductId)
      AND (@ProductName      IS NULL OR [ProductName]      LIKE @ProductName)
      AND (@ProductPackaging IS NULL OR [ProductPackaging] LIKE @ProductPackaging)
      AND (@ProductUnit      IS NULL OR [ProductUnit]      LIKE @ProductUnit)
      AND (@BatchNumber      IS NULL OR [BatchNumber]      LIKE @BatchNumber)
      AND (@ProductPcsInCtn IS NULL OR [ProductPcsInCtn] = @ProductPcsInCtn)
      AND (@ProductNetto IS NULL OR [ProductNetto] = @ProductNetto)
      AND (@SaldoAwal    IS NULL OR [SaldoAwal]    = @SaldoAwal)
      AND (@SaldoMasukPO IS NULL OR [SaldoMasukPO] = @SaldoMasukPO)
      AND (@SaldoAkhir   IS NULL OR [SaldoAkhir]   = @SaldoAkhir)
      AND (@ExpiredDate IS NULL OR CAST([ExpiredDate] AS DATE) = CAST(@ExpiredDate AS DATE))
      AND (@CreatedDate IS NULL OR CAST([CreatedDate] AS DATE) = CAST(@CreatedDate AS DATE));
END
