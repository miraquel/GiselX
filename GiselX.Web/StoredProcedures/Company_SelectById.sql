CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.Id = @Id;
END
