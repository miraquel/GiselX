CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    -- Paging & sorting
    @PageNumber     INT            = 1,
    @PageSize       INT            = 10,
    @SortColumn     NVARCHAR(100)  = NULL,
    @SortOrder      NVARCHAR(4)    = 'ASC',
    -- String column filters (support LIKE via %)
    @Name           NVARCHAR(100)  = NULL,
    @BusinessUnitName NVARCHAR(100) = NULL,
    @Address        NVARCHAR(255)  = NULL,
    @ContactEmail   NVARCHAR(255)  = NULL,
    -- Integer column filters
    @Id                 INT        = NULL,
    @DeadlineDayOfMonth INT        = NULL,
    @DeadlineDaysOfWeek INT        = NULL,
    @ReminderLeadDays   INT        = NULL,
    @ItemMappingEnabled BIT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- ── DATA RESULT SET ─────────────────────────────────────────────────────────
    SELECT
        company.[Id],
        company.[Name],
        company.[BusinessUnitId],
        unit.[Name] AS [BusinessUnitName],
        company.[Address],
        company.[ContactEmail],
        company.[DeadlineDayOfMonth],
        company.[DeadlineDaysOfWeek],
        company.[ReminderLeadDays],
        company.[ItemMappingEnabled]
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE 1 = 1
      -- String filters
      AND (@Name             IS NULL OR company.[Name]    LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.[Name]       LIKE @BusinessUnitName)
      AND (@Address          IS NULL OR company.[Address] LIKE @Address)
      AND (@ContactEmail     IS NULL OR company.[ContactEmail] LIKE @ContactEmail)
      -- Integer filters
      AND (@Id                 IS NULL OR company.[Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.[DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.[DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR company.[ReminderLeadDays]   = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.[ItemMappingEnabled] = @ItemMappingEnabled)
    ORDER BY
        -- String columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name', 'Name')
            THEN company.[Name] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('businessUnitName', 'BusinessUnitName')
            THEN unit.[Name] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address', 'Address')
            THEN company.[Address] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('contactEmail', 'ContactEmail')
            THEN company.[ContactEmail] END ASC,
        -- String columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name', 'Name')
            THEN company.[Name] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('businessUnitName', 'BusinessUnitName')
            THEN unit.[Name] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address', 'Address')
            THEN company.[Address] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('contactEmail', 'ContactEmail')
            THEN company.[ContactEmail] END DESC,
        -- Integer columns ASC
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('id', 'Id')
            THEN company.[Id] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth')
            THEN company.[DeadlineDayOfMonth] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays')
            THEN company.[ReminderLeadDays] END ASC,
        -- Integer columns DESC
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('id', 'Id')
            THEN company.[Id] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth')
            THEN company.[DeadlineDayOfMonth] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays')
            THEN company.[ReminderLeadDays] END DESC,
        -- Default fallback
        company.[Name] ASC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- ── TOTAL COUNT RESULT SET ───────────────────────────────────────────────────
    SELECT COUNT(*)
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE 1 = 1
      AND (@Name             IS NULL OR company.[Name] LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.[Name] LIKE @BusinessUnitName)
      AND (@Address          IS NULL OR company.[Address] LIKE @Address)
      AND (@ContactEmail     IS NULL OR company.[ContactEmail] LIKE @ContactEmail)
      AND (@Id                 IS NULL OR company.[Id]                 = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.[DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.[DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays   IS NULL OR company.[ReminderLeadDays]   = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.[ItemMappingEnabled] = @ItemMappingEnabled);
END
