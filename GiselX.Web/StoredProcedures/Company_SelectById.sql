CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
