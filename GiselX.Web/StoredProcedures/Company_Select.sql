CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber INT = 1, @PageSize INT = 10, @SortColumn NVARCHAR(100) = NULL, @SortOrder NVARCHAR(4) = 'ASC',
    @Name NVARCHAR(100) = NULL, @BusinessUnitName NVARCHAR(100) = NULL, @ChannelName NVARCHAR(200) = NULL,
    @SalesmanName NVARCHAR(200) = NULL, @Address NVARCHAR(255) = NULL, @Location NVARCHAR(100) = NULL,
    @ContactEmail NVARCHAR(1000) = NULL, @Id INT = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = NULL, @ItemMappingEnabled BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.Location, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@ChannelName IS NULL OR channel.Name LIKE @ChannelName)
      AND (@SalesmanName IS NULL OR salesman.Name LIKE @SalesmanName)
      AND (@Address IS NULL OR company.Address LIKE @Address)
      AND (@Location IS NULL OR company.Location LIKE @Location)
      AND (@ContactEmail IS NULL OR company.ContactEmail LIKE @ContactEmail)
      AND (@Id IS NULL OR company.Id = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.DeadlineDayOfMonth = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.DeadlineDaysOfWeek = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR company.ReminderLeadDays = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.ItemMappingEnabled = @ItemMappingEnabled)
    ORDER BY
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name','Name') THEN company.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('businessUnitName','BusinessUnitName') THEN unit.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('channelName','ChannelName') THEN channel.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('salesmanName','SalesmanName') THEN salesman.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address','Address') THEN company.Address END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('location','Location') THEN company.Location END ASC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name','Name') THEN company.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('businessUnitName','BusinessUnitName') THEN unit.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('channelName','ChannelName') THEN channel.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('salesmanName','SalesmanName') THEN salesman.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address','Address') THEN company.Address END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('location','Location') THEN company.Location END DESC,
      company.Name ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    SELECT COUNT(*)
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@ChannelName IS NULL OR channel.Name LIKE @ChannelName)
      AND (@SalesmanName IS NULL OR salesman.Name LIKE @SalesmanName)
      AND (@Address IS NULL OR company.Address LIKE @Address)
      AND (@Location IS NULL OR company.Location LIKE @Location)
      AND (@ContactEmail IS NULL OR company.ContactEmail LIKE @ContactEmail)
      AND (@Id IS NULL OR company.Id = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.DeadlineDayOfMonth = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.DeadlineDaysOfWeek = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR company.ReminderLeadDays = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.ItemMappingEnabled = @ItemMappingEnabled);
END
