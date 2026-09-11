using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260904030000_CreateChannelAndSalesmanMaster")]
public class CreateChannelAndSalesmanMaster : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "Channel", "Salesman" })
        {
            migrationBuilder.CreateTable(
                name: table,
                columns: columns => new
                {
                    Id = columns.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = columns.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = columns.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = columns.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = columns.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = columns.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedDate = columns.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = columns.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: constraints => constraints.PrimaryKey($"PK_{table}", value => value.Id));

            migrationBuilder.CreateIndex(
                name: $"UX_{table}_Code",
                table: table,
                column: "Code",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: $"{table}Id",
                table: "Company",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: $"IX_Company_{table}Id",
                table: "Company",
                column: $"{table}Id");

            migrationBuilder.AddForeignKey(
                name: $"FK_Company_{table}_{table}Id",
                table: "Company",
                column: $"{table}Id",
                principalTable: table,
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        foreach (var procedure in new[]
                 {
                     CompanyInsert, CompanyUpdate, CompanySelectById, CompanySelect, CompanySelectForReminder
                 })
            migrationBuilder.Sql(IsolateProcedureBatch(procedure));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var procedure in new[]
                 {
                     PreviousCompanyInsert, PreviousCompanyUpdate, PreviousCompanySelectById,
                     PreviousCompanySelect, PreviousCompanySelectForReminder
                 })
            migrationBuilder.Sql(IsolateProcedureBatch(procedure));

        foreach (var table in new[] { "Channel", "Salesman" })
        {
            migrationBuilder.DropForeignKey(name: $"FK_Company_{table}_{table}Id", table: "Company");
            migrationBuilder.DropIndex(name: $"IX_Company_{table}Id", table: "Company");
            migrationBuilder.DropColumn(name: $"{table}Id", table: "Company");
            migrationBuilder.DropTable(name: table);
        }
    }

    private static string IsolateProcedureBatch(string sql) => $"EXEC(N'{sql.Replace("'", "''")}');";

    private const string CompanyInsert = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @ContactEmail NVARCHAR(255) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Company]
        (Name, BusinessUnitId, ChannelId, SalesmanId, Address, ContactEmail, DeadlineDayOfMonth,
         DeadlineDaysOfWeek, ReminderLeadDays, ItemMappingEnabled)
    VALUES
        (@Name, @BusinessUnitId, @ChannelId, @SalesmanId, @Address, @ContactEmail, @DeadlineDayOfMonth,
         @DeadlineDaysOfWeek, @ReminderLeadDays, @ItemMappingEnabled);
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = SCOPE_IDENTITY();
END
""";

    private const string CompanyUpdate = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id INT, @Name NVARCHAR(100), @BusinessUnitId INT = NULL, @ChannelId INT = NULL, @SalesmanId INT = NULL,
    @Address NVARCHAR(255) = NULL, @ContactEmail NVARCHAR(255) = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = 3, @ItemMappingEnabled BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Company]
    SET Name = @Name, BusinessUnitId = @BusinessUnitId, ChannelId = @ChannelId, SalesmanId = @SalesmanId,
        Address = @Address, ContactEmail = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth, DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays = @ReminderLeadDays, ItemMappingEnabled = @ItemMappingEnabled
    WHERE Id = @Id;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = @Id;
END
""";

    private const string CompanySelectById = """
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById] @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.Id = @Id;
END
""";

    private const string CompanySelect = """
CREATE OR ALTER PROCEDURE [dbo].[Company_Select]
    @PageNumber INT = 1, @PageSize INT = 10, @SortColumn NVARCHAR(100) = NULL, @SortOrder NVARCHAR(4) = 'ASC',
    @Name NVARCHAR(100) = NULL, @BusinessUnitName NVARCHAR(100) = NULL, @ChannelName NVARCHAR(200) = NULL,
    @SalesmanName NVARCHAR(200) = NULL, @Address NVARCHAR(255) = NULL,
    @ContactEmail NVARCHAR(255) = NULL, @Id INT = NULL, @DeadlineDayOfMonth INT = NULL,
    @DeadlineDaysOfWeek INT = NULL, @ReminderLeadDays INT = NULL, @ItemMappingEnabled BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SELECT company.Id, company.Name, company.BusinessUnitId, unit.Name AS BusinessUnitName,
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth, company.DeadlineDaysOfWeek,
           company.ReminderLeadDays, company.ItemMappingEnabled
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@ChannelName IS NULL OR channel.Name LIKE @ChannelName)
      AND (@SalesmanName IS NULL OR salesman.Name LIKE @SalesmanName)
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
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('channelName','ChannelName') THEN channel.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('salesmanName','SalesmanName') THEN salesman.Name END ASC,
      CASE WHEN @SortOrder = 'ASC' AND @SortColumn IN ('address','Address') THEN company.Address END ASC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('name','Name') THEN company.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('businessUnitName','BusinessUnitName') THEN unit.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('channelName','ChannelName') THEN channel.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('salesmanName','SalesmanName') THEN salesman.Name END DESC,
      CASE WHEN @SortOrder = 'DESC' AND @SortColumn IN ('address','Address') THEN company.Address END DESC,
      company.Name ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    SELECT COUNT(*)
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE (@Name IS NULL OR company.Name LIKE @Name)
      AND (@BusinessUnitName IS NULL OR unit.Name LIKE @BusinessUnitName)
      AND (@ChannelName IS NULL OR channel.Name LIKE @ChannelName)
      AND (@SalesmanName IS NULL OR salesman.Name LIKE @SalesmanName)
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
           company.ChannelId, channel.Name AS ChannelName,
           company.SalesmanId, salesman.Name AS SalesmanName,
           company.Address, company.ContactEmail, company.DeadlineDayOfMonth,
           company.DeadlineDaysOfWeek, company.ReminderLeadDays
    FROM [dbo].[Company] company
    LEFT JOIN [dbo].[BusinessUnit] unit ON unit.Id = company.BusinessUnitId
    LEFT JOIN [dbo].[Channel] channel ON channel.Id = company.ChannelId
    LEFT JOIN [dbo].[Salesman] salesman ON salesman.Id = company.SalesmanId
    WHERE company.ContactEmail IS NOT NULL
      AND (company.DeadlineDayOfMonth IS NOT NULL OR company.DeadlineDaysOfWeek IS NOT NULL);
END
""";

    private const string PreviousCompanyInsert = """
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

    private const string PreviousCompanyUpdate = """
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

    private const string PreviousCompanySelectById = """
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

    private const string PreviousCompanySelect = """
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

    private const string PreviousCompanySelectForReminder = """
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
