using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260827120000_CreateBusinessUnitMaster")]
public class CreateBusinessUnitMaster : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BusinessUnit",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_BusinessUnit", value => value.Id));

        migrationBuilder.CreateIndex(
            name: "IX_BusinessUnit_Name",
            table: "BusinessUnit",
            column: "Name",
            unique: true);

        migrationBuilder.AddColumn<int>(
            name: "BusinessUnitId",
            table: "Company",
            type: "int",
            nullable: true);

        migrationBuilder.Sql("""
            INSERT INTO [dbo].[BusinessUnit] ([Name], [IsActive], [CreatedDate], [CreatedBy])
            SELECT DISTINCT LTRIM(RTRIM([BusinessUnit])), 1, SYSUTCDATETIME(), N'migration'
            FROM [dbo].[Company]
            WHERE NULLIF(LTRIM(RTRIM([BusinessUnit])), N'') IS NOT NULL;

            UPDATE company
            SET [BusinessUnitId] = unit.[Id]
            FROM [dbo].[Company] company
            INNER JOIN [dbo].[BusinessUnit] unit
                ON unit.[Name] = LTRIM(RTRIM(company.[BusinessUnit]));
            """);

        migrationBuilder.DropColumn(name: "BusinessUnit", table: "Company");

        migrationBuilder.CreateIndex(
            name: "IX_Company_BusinessUnitId",
            table: "Company",
            column: "BusinessUnitId");

        migrationBuilder.AddForeignKey(
            name: "FK_Company_BusinessUnit_BusinessUnitId",
            table: "Company",
            column: "BusinessUnitId",
            principalTable: "BusinessUnit",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        foreach (var procedure in new[]
                 {
                     CompanyInsert, CompanyUpdate, CompanySelectById, CompanySelect, CompanySelectForReminder
                 })
            migrationBuilder.Sql(IsolateProcedureBatch(procedure));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessUnit",
            table: "Company",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE company
            SET [BusinessUnit] = unit.[Name]
            FROM [dbo].[Company] company
            LEFT JOIN [dbo].[BusinessUnit] unit ON unit.[Id] = company.[BusinessUnitId];
            """);

        AddCompanyBusinessUnit.InstallProcedures(migrationBuilder, includeBusinessUnit: true);

        migrationBuilder.DropForeignKey(name: "FK_Company_BusinessUnit_BusinessUnitId", table: "Company");
        migrationBuilder.DropIndex(name: "IX_Company_BusinessUnitId", table: "Company");
        migrationBuilder.DropColumn(name: "BusinessUnitId", table: "Company");
        migrationBuilder.DropTable(name: "BusinessUnit");
    }

    private static string IsolateProcedureBatch(string sql) => $"EXEC(N'{sql.Replace("'", "''")}');";

    private const string CompanyInsert = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @Address NVARCHAR(255) = NULL,
    @ContactEmail NVARCHAR(255) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Company]
        (Name, BusinessUnitId, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES
        (@Name, @BusinessUnitId, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.Id = SCOPE_IDENTITY();
END
""";

    private const string CompanyUpdate = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id INT, @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @Address NVARCHAR(255) = NULL,
    @ContactEmail NVARCHAR(255) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Company]
    SET Name = @Name, BusinessUnitId = @BusinessUnitId, Address = @Address, ContactEmail = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth, DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays = @ReminderLeadDays, ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.Id = @Id;
END
""";

    private const string CompanySelectById = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById] @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.Id = @Id;
END
""";

    private const string CompanySelect = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber INT = 1, @PageSize INT = 10, @SortColumn NVARCHAR(100) = NULL, @SortOrder NVARCHAR(4) = 'ASC',
    @Name NVARCHAR(100) = NULL, @BusinessUnitName NVARCHAR(100) = NULL, @Address NVARCHAR(255) = NULL,
    @ContactEmail NVARCHAR(255) = NULL, @Id INT = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = NULL, @ItemMappingEnabled BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@Address IS NULL OR company.Address LIKE @Address)
      AND (@ContactEmail IS NULL OR company.ContactEmail LIKE @ContactEmail)
      AND (@Id IS NULL OR company.Id = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.DeadlineDayOfMonth = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.DeadlineDaysOfWeek = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR company.ReminderLeadDays = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.ItemMappingEnabled = @ItemMappingEnabled)
    ORDER BY
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('name','Name') THEN company.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('businessUnitName','BusinessUnitName') THEN unit.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address','Address') THEN company.Address END ASC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name','Name') THEN company.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('businessUnitName','BusinessUnitName') THEN unit.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address','Address') THEN company.Address END DESC,
      company.Name ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    SELECT COUNT(*)
    FROM [dbo].[Company] company LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@Address IS NULL OR company.Address LIKE @Address)
      AND (@ContactEmail IS NULL OR company.ContactEmail LIKE @ContactEmail)
      AND (@Id IS NULL OR company.Id = @Id)
      AND (@DeadlineDayOfMonth IS NULL OR company.DeadlineDayOfMonth = @DeadlineDayOfMonth)
      AND (@DeadlineDaysOfWeek IS NULL OR company.DeadlineDaysOfWeek = @DeadlineDaysOfWeek)
      AND (@ReminderLeadDays IS NULL OR company.ReminderLeadDays = @ReminderLeadDays)
      AND (@ItemMappingEnabled IS NULL OR company.ItemMappingEnabled = @ItemMappingEnabled);
END
""";

    private const string CompanySelectForReminder = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth,
           company.DeadlineDaysOfWeek, company.ReminderLeadDays
    FROM [dbo].[Company] company LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    WHERE company.ContactEmail IS NOT NULL
      AND (company.DeadlineDayOfMonth IS NOT NULL OR company.DeadlineDaysOfWeek IS NOT NULL);
END
""";
}
