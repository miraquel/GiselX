CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE ContactEmail IS NOT NULL
      AND (DeadlineDayOfMonth IS NOT NULL OR DeadlineDaysOfWeek IS NOT NULL);
END
