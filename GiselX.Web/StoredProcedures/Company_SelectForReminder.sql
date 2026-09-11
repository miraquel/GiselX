CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth,
           company.DeadlineDaysOfWeek, company.ReminderLeadDays
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.ContactEmail IS NOT NULL
      AND (company.DeadlineDayOfMonth IS NOT NULL OR company.DeadlineDaysOfWeek IS NOT NULL);
END
