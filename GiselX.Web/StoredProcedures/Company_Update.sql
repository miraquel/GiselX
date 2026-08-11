CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id                 INT,
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

    UPDATE [dbo].[Company]
    SET Name               = @Name,
        Address            = @Address,
        ContactEmail       = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth,
        DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays   = @ReminderLeadDays,
        ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
