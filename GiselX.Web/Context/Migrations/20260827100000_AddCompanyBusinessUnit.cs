using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260827100000_AddCompanyBusinessUnit")]
public class AddCompanyBusinessUnit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessUnit",
            table: "Company",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        InstallProcedures(migrationBuilder, includeBusinessUnit: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        InstallProcedures(migrationBuilder, includeBusinessUnit: false);

        migrationBuilder.DropColumn(
            name: "BusinessUnit",
            table: "Company");
    }

    internal static void InstallProcedures(MigrationBuilder migrationBuilder, bool includeBusinessUnit)
    {
        var procedures = includeBusinessUnit
            ? new[] { CompanyInsert, CompanyUpdate, CompanySelectById, CompanySelect, CompanySelectForReminder }
            : new[] { PreviousCompanyInsert, PreviousCompanyUpdate, PreviousCompanySelectById, PreviousCompanySelect,
                PreviousCompanySelectForReminder };

        foreach (var procedure in procedures)
            migrationBuilder.Sql(IsolateProcedureBatch(procedure));
    }

    private static string IsolateProcedureBatch(string sql) => $"EXEC(N'{sql.Replace("'", "''")}');";

    private const string CompanyInsert = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name               NVARCHAR(100),
    @BusinessUnit       NVARCHAR(100) = NULL,
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3,
    @ItemMappingEnabled BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Company]
        (Name, BusinessUnit, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES
        (@Name, @BusinessUnit, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);

    SELECT Id, Name, BusinessUnit, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = SCOPE_IDENTITY();
END
""";

    private const string CompanyUpdate = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id                 INT,
    @Name               NVARCHAR(100),
    @BusinessUnit       NVARCHAR(100) = NULL,
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
        BusinessUnit       = @BusinessUnit,
        Address            = @Address,
        ContactEmail       = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth,
        DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays   = @ReminderLeadDays,
        ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;

    SELECT Id, Name, BusinessUnit, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
""";

    private const string CompanySelectById = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, BusinessUnit, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
""";

    private const string CompanySelect = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber         INT            = 1,
    @PageSize           INT            = 10,
    @SortColumn         NVARCHAR(100)  = NULL,
    @SortOrder          NVARCHAR(4)    = 'ASC',
    @Name               NVARCHAR(100)  = NULL,
    @BusinessUnit       NVARCHAR(100)  = NULL,
    @Address            NVARCHAR(255)  = NULL,
    @ContactEmail       NVARCHAR(255)  = NULL,
    @Id                 INT            = NULL,
    @DeadlineDayOfMonth INT            = NULL,
    @DeadlineDaysOfWeek INT            = NULL,
    @ReminderLeadDays   INT            = NULL,
    @ItemMappingEnabled BIT            = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT [Id], [Name], [BusinessUnit], [Address], [ContactEmail], [DeadlineDayOfMonth],
           [DeadlineDaysOfWeek], [ReminderLeadDays], [ItemMappingEnabled]
    FROM [dbo].[Company]
    WHERE (@Name IS NULL OR [Name] LIKE @Name)
      AND (@BusinessUnit IS NULL OR [BusinessUnit] LIKE @BusinessUnit)
      AND (@Address IS NULL OR [Address] LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id IS NULL OR [Id] = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR [ReminderLeadDays] = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled)
    ORDER BY
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name', 'Name') THEN [Name] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('businessUnit', 'BusinessUnit') THEN [BusinessUnit] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address', 'Address') THEN [Address] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('contactEmail', 'ContactEmail') THEN [ContactEmail] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name', 'Name') THEN [Name] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('businessUnit', 'BusinessUnit') THEN [BusinessUnit] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address', 'Address') THEN [Address] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('contactEmail', 'ContactEmail') THEN [ContactEmail] END DESC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('id', 'Id') THEN [Id] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth') THEN [DeadlineDayOfMonth] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays') THEN [ReminderLeadDays] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('id', 'Id') THEN [Id] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth') THEN [DeadlineDayOfMonth] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays') THEN [ReminderLeadDays] END DESC,
        [Name] ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*)
    FROM [dbo].[Company]
    WHERE (@Name IS NULL OR [Name] LIKE @Name)
      AND (@BusinessUnit IS NULL OR [BusinessUnit] LIKE @BusinessUnit)
      AND (@Address IS NULL OR [Address] LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id IS NULL OR [Id] = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR [ReminderLeadDays] = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled);
END
""";

    private const string CompanySelectForReminder = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, BusinessUnit, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE ContactEmail IS NOT NULL
      AND (DeadlineDayOfMonth IS NOT NULL OR DeadlineDaysOfWeek IS NOT NULL);
END
""";

    private const string PreviousCompanyInsert = """
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
    FROM [dbo].[Company] WHERE Id = SCOPE_IDENTITY();
END
""";

    private const string PreviousCompanyUpdate = """
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
    SET Name = @Name, Address = @Address, ContactEmail = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth, DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays = @ReminderLeadDays, ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;
    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company] WHERE Id = @Id;
END
""";

    private const string PreviousCompanySelectById = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled
    FROM [dbo].[Company] WHERE Id = @Id;
END
""";

    private const string PreviousCompanySelect = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber         INT            = 1,
    @PageSize           INT            = 10,
    @SortColumn         NVARCHAR(100)  = NULL,
    @SortOrder          NVARCHAR(4)    = 'ASC',
    @Name               NVARCHAR(100)  = NULL,
    @Address            NVARCHAR(255)  = NULL,
    @ContactEmail       NVARCHAR(255)  = NULL,
    @Id                 INT            = NULL,
    @DeadlineDayOfMonth INT            = NULL,
    @DeadlineDaysOfWeek INT            = NULL,
    @ReminderLeadDays   INT            = NULL,
    @ItemMappingEnabled BIT            = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SELECT [Id], [Name], [Address], [ContactEmail], [DeadlineDayOfMonth], [DeadlineDaysOfWeek],
           [ReminderLeadDays], [ItemMappingEnabled]
    FROM [dbo].[Company]
    WHERE (@Name IS NULL OR [Name] LIKE @Name)
      AND (@Address IS NULL OR [Address] LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id IS NULL OR [Id] = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR [ReminderLeadDays] = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled)
    ORDER BY
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name', 'Name') THEN [Name] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address', 'Address') THEN [Address] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('contactEmail', 'ContactEmail') THEN [ContactEmail] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name', 'Name') THEN [Name] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address', 'Address') THEN [Address] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('contactEmail', 'ContactEmail') THEN [ContactEmail] END DESC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('id', 'Id') THEN [Id] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth') THEN [DeadlineDayOfMonth] END ASC,
        CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays') THEN [ReminderLeadDays] END ASC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('id', 'Id') THEN [Id] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('deadlineDayOfMonth', 'DeadlineDayOfMonth') THEN [DeadlineDayOfMonth] END DESC,
        CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('reminderLeadDays', 'ReminderLeadDays') THEN [ReminderLeadDays] END DESC,
        [Name] ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    SELECT COUNT(*) FROM [dbo].[Company]
    WHERE (@Name IS NULL OR [Name] LIKE @Name)
      AND (@Address IS NULL OR [Address] LIKE @Address)
      AND (@ContactEmail IS NULL OR [ContactEmail] LIKE @ContactEmail)
      AND (@Id IS NULL OR [Id] = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR [DeadlineDayOfMonth] = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR [DeadlineDaysOfWeek] = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR [ReminderLeadDays] = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR [ItemMappingEnabled] = @ItemMappingEnabled);
END
""";

    private const string PreviousCompanySelectForReminder = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE ContactEmail IS NOT NULL
      AND (DeadlineDayOfMonth IS NOT NULL OR DeadlineDaysOfWeek IS NOT NULL);
END
""";
}
