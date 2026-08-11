CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Company] (Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES (@Name, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = SCOPE_IDENTITY();
END
