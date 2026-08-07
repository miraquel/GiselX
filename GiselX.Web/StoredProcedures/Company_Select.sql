CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    -- Paging & sorting
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- String column filters (support LIKE via %)
    @Name           NVARCHAR(100)  = NULL,
    @Address        NVARCHAR(255)  = NULL,
    @ContactEmail   NVARCHAR(255)  = NULL,
    -- Integer column filters
    @Id                 INT        = NULL,
    @DeadlineDayOfMonth INT        = NULL,
    @DeadlineDaysOfWeek INT        = NULL,
    @ReminderLeadDays   INT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- ── DATA RESULT SET ─────────────────────────────────────────────────────────
    SELECT
        [Id],
        [Name],
        [Address],
        [ContactEmail],
        [DeadlineDayOfMonth],
        [DeadlineDaysOfWeek],
        [ReminderLeadDays]
    FROM [dbo].[Company]
    WHERE 1 = 1
      -- String filters
      AND (@Name         IS NULL OR [Name]         LIKE @Name)
      AND (@Address      IS NULL OR [Address]      LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      -- Integer filters
      AND (@Id                 IS NULL OR [Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR [ReminderLeadDays]   = @ReminderLeadDays)
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name', 'Name')
            THEN [Name]         END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address', 'Address')
            THEN [Address]      END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('contactEmail', 'ContactEmail')
            THEN [ContactEmail] END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name', 'Name')
            THEN [Name]         END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address', 'Address')
            THEN [Address]      END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('contactEmail', 'ContactEmail')
            THEN [ContactEmail] END DESC,
        -- Integer columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('id', 'Id')
            THEN [Id]                 END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth')
            THEN [DeadlineDayOfMonth] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays')
            THEN [ReminderLeadDays]   END ASC,
        -- Integer columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('id', 'Id')
            THEN [Id]                 END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth')
            THEN [DeadlineDayOfMonth] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays')
            THEN [ReminderLeadDays]   END DESC,
        -- Default fallback
        [Name] ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- ── TOTAL COUNT RESULT SET ───────────────────────────────────────────────────
    SELECT COUNT(*)
    FROM [dbo].[Company]
    WHERE 1 = 1
      AND (@Name         IS NULL OR [Name]         LIKE @Name)
      AND (@Address      IS NULL OR [Address]      LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id                 IS NULL OR [Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR [ReminderLeadDays]   = @ReminderLeadDays);
END
