CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @Location NVARCHAR(100) = NULL, @ContactEmail NVARCHAR(1000) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Company]
        (Name, BusinessUnitId, ChannelId, SalesmanId, Address, Location, ContactEmail, DeadlineDayOfMonth,
         DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES
        (@Name, @BusinessUnitId, @ChannelId, @SalesmanId, @Address, @Location, @ContactEmail, @DeadlineDayOfMonth,
         @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.Location, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = SCOPE_IDENTITY();
END
