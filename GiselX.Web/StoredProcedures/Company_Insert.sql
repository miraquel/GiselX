CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name               NVARCHAR(100),
    @BusinessUnitId     INT           = NULL,
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Company] (Name, BusinessUnitId, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES (@Name, @BusinessUnitId, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);

    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.Id = SCOPE_IDENTITY();
END
