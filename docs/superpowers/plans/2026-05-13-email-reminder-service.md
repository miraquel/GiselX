# Email Reminder Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Hangfire-scheduled background job that sends MailKit SMTP reminder emails to distributors who have not uploaded SalesTransaction, Stock, and TransDist data before their configured deadline.

**Architecture:** A `ReminderJob` Hangfire recurring job (daily 08:00) loads all configured Company records, determines if today is an early-warning or final-reminder trigger day, checks upload status via Dapper EXISTS queries against the three data tables, and sends emails via MailKit SMTP. Deadline is configured per company as either a day-of-month (monthly cycle) or a bitmask of weekdays (weekly cycle). All new components follow the existing layered pattern: Domain → Repository.Interface → Repository → Service.Interface → Service → Web.

**Tech Stack:** ASP.NET Core .NET 10, SQL Server, EF Core (migration only), Dapper, Hangfire.AspNetCore + Hangfire.SqlServer, MailKit, Microsoft.Extensions.Options

---

## Codebase Notes

- **Repositories use Dapper** via `IDbConnection` + `IDbTransaction` (not EF Core). All queries go through stored procedures, **except** `UploadCheckRepository` which uses inline SQL (a simple EXISTS check does not warrant a stored procedure).
- **`ServiceLevel` data lives in the `TransDist` SQL table** — confirmed by `Gisel_Select.sql`. The `SalesTransaction` table name is `SalesTransaction` and `Stock` is `Stock` (confirmed by `SqlBulkCopy.DestinationTableName` in their repositories).
- **`GiselX.Service.Dto` has no project references** — `WeekDays` enum lives in `GiselX.Domain`, so `CompanyDto.DeadlineDaysOfWeek` uses `int?`. Mapperly handles `int?` ↔ `WeekDays?` via implicit cast.
- **No test project exists** — tasks include manual verification steps instead of automated tests.
- **`CompaniesController` is in `Areas/Admin`** — views live at `GiselX.Web/Areas/Admin/Views/Companies/`.
- **Authorization uses permission claims** (`"permission"` claim), not roles. Add `Hangfire.Dashboard` permission to `PermissionConstants`.

---

## File Map

### New Files

| File | Responsibility |
|---|---|
| `GiselX.Domain/WeekDays.cs` | `[Flags]` enum for day-of-week bitmask |
| `GiselX.Repository.Interface/IUploadCheckRepository.cs` | Contract: check if all 3 tables have data in a date range |
| `GiselX.Repository/UploadCheckRepository.cs` | Dapper inline EXISTS queries against SalesTransaction, Stock, TransDist |
| `GiselX.Service.Interface/IEmailService.cs` | Contract: send a reminder email |
| `GiselX.Service.Interface/IUploadCheckService.cs` | Contract: check uploads for current period |
| `GiselX.Service/EmailSettings.cs` | Typed config POCO bound to `"Email"` section of appsettings |
| `GiselX.Service/EmailService.cs` | MailKit SMTP implementation |
| `GiselX.Service/UploadCheckService.cs` | Resolves date range (monthly/weekly ISO week), delegates to repository |
| `GiselX.Service/ReminderJob.cs` | Hangfire job: orchestrates deadline check → upload check → email |
| `GiselX.Web/HangfireAdminAuthFilter.cs` | Restricts `/hangfire` dashboard to users with `Hangfire.Dashboard` permission claim |
| `GiselX.Web/StoredProcedures/Company_Insert.sql` | Updated SP: accepts new deadline columns |
| `GiselX.Web/StoredProcedures/Company_Update.sql` | Updated SP: accepts new deadline columns |
| `GiselX.Web/StoredProcedures/Company_SelectById.sql` | Updated SP: returns new deadline columns |
| `GiselX.Web/StoredProcedures/Company_SelectForReminder.sql` | New SP: returns companies with ContactEmail configured |

### Modified Files

| File | Change |
|---|---|
| `GiselX.Domain/Company.cs` | Add `ContactEmail`, `DeadlineDayOfMonth`, `DeadlineDaysOfWeek`, `ReminderLeadDays` |
| `GiselX.Service.Dto/CompanyDto.cs` | Add same 4 fields (`int?` for `DeadlineDaysOfWeek`) |
| `GiselX.Repository.Interface/ICompanyRepository.cs` | Add `GetAllWithContactEmailAsync` |
| `GiselX.Repository/CompanyRepository.cs` | Update Create/Update params; add `GetAllWithContactEmailAsync` |
| `GiselX.Repository/ServiceCollectionExtensions.cs` | Register `IUploadCheckRepository` |
| `GiselX.Service/ServiceCollectionExtensions.cs` | Register `IEmailService`, `IUploadCheckService`, `ReminderJob` |
| `GiselX.Common/Constants/PermissionConstants.cs` | Add `Hangfire.Dashboard` permission |
| `GiselX.Web/Context/GiselXDbContext.cs` | Configure `ContactEmail` max length + `ReminderLeadDays` default |
| `GiselX.Web/Program.cs` | Add Hangfire, `Configure<EmailSettings>`, dashboard route, cron job |
| `GiselX.Web/GiselX.Web.csproj` | Add `Hangfire.AspNetCore`, `Hangfire.SqlServer` |
| `GiselX.Service/GiselX.Service.csproj` | Add `MailKit`, `Microsoft.Extensions.Options` |
| `GiselX.Web/appsettings.json` | Add `Email` and `Hangfire` sections |
| `GiselX.Web/Areas/Admin/Views/Companies/Create.cshtml` | Add 4 new fields with JS deadline-type toggle |
| `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml` | Add 4 new fields with JS deadline-type toggle + pre-population |

---

## Task 1: Install NuGet packages

**Files:**
- Modify: `GiselX.Web/GiselX.Web.csproj`
- Modify: `GiselX.Service/GiselX.Service.csproj`

- [ ] **Step 1: Add Hangfire packages to GiselX.Web**

```powershell
cd D:\Programming\GiselX
dotnet add GiselX.Web package Hangfire.AspNetCore
dotnet add GiselX.Web package Hangfire.SqlServer
```

- [ ] **Step 2: Add MailKit and Options to GiselX.Service**

```powershell
dotnet add GiselX.Service package MailKit
dotnet add GiselX.Service package Microsoft.Extensions.Options
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add GiselX.Web/GiselX.Web.csproj GiselX.Service/GiselX.Service.csproj
git commit -m "feat: add Hangfire and MailKit NuGet packages"
```

---

## Task 2: Domain — WeekDays enum + Company entity fields

**Files:**
- Create: `GiselX.Domain/WeekDays.cs`
- Modify: `GiselX.Domain/Company.cs`

- [ ] **Step 1: Create `GiselX.Domain/WeekDays.cs`**

```csharp
namespace GiselX.Domain;

[Flags]
public enum WeekDays
{
    None      = 0,
    Sunday    = 1,
    Monday    = 2,
    Tuesday   = 4,
    Wednesday = 8,
    Thursday  = 16,
    Friday    = 32,
    Saturday  = 64
}
```

Each value is a power of 2 so multiple days can be stored in one `int` column (e.g. Monday + Thursday = `2 + 16 = 18`).

- [ ] **Step 2: Replace contents of `GiselX.Domain/Company.cs`**

```csharp
namespace GiselX.Domain;

public partial class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public int? DeadlineDayOfMonth { get; set; }
    public WeekDays? DeadlineDaysOfWeek { get; set; }
    public int ReminderLeadDays { get; set; } = 3;

    public virtual ICollection<TransDist> TransDist { get; set; } = new List<TransDist>();
    public virtual ICollection<AppIdentityUser> Users { get; set; } = new List<AppIdentityUser>();
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.Domain
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add GiselX.Domain/WeekDays.cs GiselX.Domain/Company.cs
git commit -m "feat: add WeekDays enum and deadline fields to Company entity"
```

---

## Task 3: EF Core migration

**Files:**
- Modify: `GiselX.Web/Context/GiselXDbContext.cs`
- Create: `GiselX.Web/Context/Migrations/<timestamp>_AddCompanyReminderFields.cs` (auto-generated)

- [ ] **Step 1: Update `OnModelCreating` in `GiselXDbContext.cs`**

Inside the existing `modelBuilder.Entity<Company>(entity => { ... })` block, add the three new property configurations:

```csharp
modelBuilder.Entity<Company>(entity =>
{
    entity.HasIndex(e => e.Name).IsUnique();
    entity.Property(e => e.Address).HasMaxLength(255);
    entity.Property(e => e.Name).HasMaxLength(100);

    // new
    entity.Property(e => e.ContactEmail).HasMaxLength(255);
    entity.Property(e => e.ReminderLeadDays).HasDefaultValue(3);
    // DeadlineDayOfMonth and DeadlineDaysOfWeek are int? — EF maps them to INT NULL automatically
});
```

- [ ] **Step 2: Add the EF migration**

```powershell
dotnet ef migrations add AddCompanyReminderFields --project GiselX.Web --context GiselXDbContext
```

Expected: A new file created under `GiselX.Web/Context/Migrations/`.

- [ ] **Step 3: Inspect the generated migration file**

Open the generated `..._AddCompanyReminderFields.cs` and verify it contains:
- `AddColumn` for `ContactEmail` (nullable, maxLength 255)
- `AddColumn` for `DeadlineDayOfMonth` (nullable int)
- `AddColumn` for `DeadlineDaysOfWeek` (nullable int)
- `AddColumn` for `ReminderLeadDays` (int, defaultValue 3)

If anything is missing or wrong, delete the migration file and the snapshot entry, fix `GiselXDbContext.cs`, and re-run Step 2.

- [ ] **Step 4: Apply the migration**

```powershell
dotnet ef database update --project GiselX.Web --context GiselXDbContext
```

Expected: `Done. 0 migration(s) failed.`

- [ ] **Step 5: Commit**

```powershell
git add GiselX.Web/Context/
git commit -m "feat: EF migration - add deadline and contact fields to Company table"
```

---

## Task 4: Company stored procedures

**Files:**
- Create: `GiselX.Web/StoredProcedures/Company_Insert.sql`
- Create: `GiselX.Web/StoredProcedures/Company_Update.sql`
- Create: `GiselX.Web/StoredProcedures/Company_SelectById.sql`
- Create: `GiselX.Web/StoredProcedures/Company_SelectForReminder.sql`

- [ ] **Step 1: Create `Company_Insert.sql`**

```sql
CREATE OR ALTER PROCEDURE [dbo].[Company_Insert]
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Company] (Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays)
    VALUES (@Name, @Address, @ContactEmail, @DeadlineDayOfMonth, @DeadlineDaysOfWeek, @ReminderLeadDays);

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE Id = SCOPE_IDENTITY();
END
```

- [ ] **Step 2: Create `Company_Update.sql`**

```sql
CREATE OR ALTER PROCEDURE [dbo].[Company_Update]
    @Id                 INT,
    @Name               NVARCHAR(100),
    @Address            NVARCHAR(255) = NULL,
    @ContactEmail       NVARCHAR(255) = NULL,
    @DeadlineDayOfMonth INT           = NULL,
    @DeadlineDaysOfWeek INT           = NULL,
    @ReminderLeadDays   INT           = 3
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Company]
    SET Name               = @Name,
        Address            = @Address,
        ContactEmail       = @ContactEmail,
        DeadlineDayOfMonth = @DeadlineDayOfMonth,
        DeadlineDaysOfWeek = @DeadlineDaysOfWeek,
        ReminderLeadDays   = @ReminderLeadDays
    WHERE Id = @Id;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
```

- [ ] **Step 3: Create `Company_SelectById.sql`**

```sql
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE Id = @Id;
END
```

- [ ] **Step 4: Create `Company_SelectForReminder.sql`**

```sql
CREATE OR ALTER PROCEDURE [dbo].[Company_SelectForReminder]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Address, ContactEmail, DeadlineDayOfMonth, DeadlineDaysOfWeek, ReminderLeadDays
    FROM [dbo].[Company]
    WHERE ContactEmail IS NOT NULL
      AND (DeadlineDayOfMonth IS NOT NULL OR DeadlineDaysOfWeek IS NOT NULL);
END
```

- [ ] **Step 5: Execute all four SQL files against the database**

Connect to `Server=<DB-SERVER>; Database=GiselXDb` using SQL Server Management Studio or Azure Data Studio. Run each `.sql` file. All four should complete with `Command(s) completed successfully.`

- [ ] **Step 6: Commit**

```powershell
git add GiselX.Web/StoredProcedures/
git commit -m "feat: update Company stored procedures for deadline and contact fields"
```

---

## Task 5: CompanyDto update

**Files:**
- Modify: `GiselX.Service.Dto/CompanyDto.cs`

> `DeadlineDaysOfWeek` is `int?` here because `GiselX.Service.Dto` has no reference to `GiselX.Domain`. Mapperly auto-handles the `int?` ↔ `WeekDays?` cast since both share the same underlying type (`int`).

- [ ] **Step 1: Replace `GiselX.Service.Dto/CompanyDto.cs`**

```csharp
namespace GiselX.Service.Dto;

public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public int? DeadlineDayOfMonth { get; set; }
    public int? DeadlineDaysOfWeek { get; set; }
    public int ReminderLeadDays { get; set; } = 3;
}
```

- [ ] **Step 2: Build the full solution — verify Mapperly compiles**

```powershell
dotnet build GiselX.sln
```

Expected: `Build succeeded. 0 Error(s)`

If Mapperly emits a build error about mapping `int?` to `WeekDays?`, open `GiselX.Mapper/MapperlyMapper.cs` and add these two private conversion helpers **inside the `MapperlyMapper` class**. Mapperly picks these up automatically:

```csharp
private static GiselX.Domain.WeekDays? MapIntToWeekDays(int? value) =>
    value.HasValue ? (GiselX.Domain.WeekDays)value.Value : null;

private static int? MapWeekDaysToInt(GiselX.Domain.WeekDays? value) =>
    value.HasValue ? (int)value.Value : null;
```

- [ ] **Step 3: Commit**

```powershell
git add GiselX.Service.Dto/CompanyDto.cs GiselX.Mapper/MapperlyMapper.cs
git commit -m "feat: add deadline and contact fields to CompanyDto"
```

---

## Task 6: UploadCheckRepository

**Files:**
- Create: `GiselX.Repository.Interface/IUploadCheckRepository.cs`
- Create: `GiselX.Repository/UploadCheckRepository.cs`

- [ ] **Step 1: Create `GiselX.Repository.Interface/IUploadCheckRepository.cs`**

```csharp
namespace GiselX.Repository.Interface;

public interface IUploadCheckRepository
{
    Task<bool> HasDataForPeriodAsync(int companyId, DateTime from, DateTime to);
}
```

- [ ] **Step 2: Create `GiselX.Repository/UploadCheckRepository.cs`**

```csharp
using System.Data;
using Dapper;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public class UploadCheckRepository : IUploadCheckRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public UploadCheckRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public async Task<bool> HasDataForPeriodAsync(int companyId, DateTime from, DateTime to)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(1) FROM [dbo].[SalesTransaction] WHERE CompanyId = @CompanyId AND CreatedDate >= @From AND CreatedDate < @To),
                (SELECT COUNT(1) FROM [dbo].[Stock]            WHERE CompanyId = @CompanyId AND CreatedDate >= @From AND CreatedDate < @To),
                (SELECT COUNT(1) FROM [dbo].[TransDist]        WHERE CompanyId = @CompanyId AND CreatedDate >= @From AND CreatedDate < @To)
            """;

        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var result = await _dbConnection.QuerySingleAsync<(int Sales, int Stock, int TransDist)>(
            sql,
            new { CompanyId = companyId, From = from, To = to },
            _dbTransaction);

        return result.Sales > 0 && result.Stock > 0 && result.TransDist > 0;
    }
}
```

> `TransDist` is the physical table for ServiceLevel/distribution data — confirmed by `Gisel_Select.sql`. `SalesTransaction` and `Stock` table names are confirmed by `SqlBulkCopy.DestinationTableName` in their respective repositories.

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.Repository
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add GiselX.Repository.Interface/IUploadCheckRepository.cs GiselX.Repository/UploadCheckRepository.cs
git commit -m "feat: add UploadCheckRepository with EXISTS check across all three data tables"
```

---

## Task 7: UploadCheckService

**Files:**
- Create: `GiselX.Service.Interface/IUploadCheckService.cs`
- Create: `GiselX.Service/UploadCheckService.cs`

- [ ] **Step 1: Create `GiselX.Service.Interface/IUploadCheckService.cs`**

```csharp
namespace GiselX.Service.Interface;

public interface IUploadCheckService
{
    Task<bool> HasUploadedThisPeriodAsync(int companyId, bool isWeekly);
}
```

- [ ] **Step 2: Create `GiselX.Service/UploadCheckService.cs`**

```csharp
using GiselX.Repository.Interface;
using GiselX.Service.Interface;

namespace GiselX.Service;

public class UploadCheckService : IUploadCheckService
{
    private readonly IUploadCheckRepository _repository;

    public UploadCheckService(IUploadCheckRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> HasUploadedThisPeriodAsync(int companyId, bool isWeekly)
    {
        var (from, to) = isWeekly ? GetCurrentIsoWeek() : GetCurrentMonth();
        return _repository.HasDataForPeriodAsync(companyId, from, to);
    }

    private static (DateTime From, DateTime To) GetCurrentMonth()
    {
        var today = DateTime.Today;
        var from = new DateTime(today.Year, today.Month, 1);
        return (from, from.AddMonths(1));
    }

    private static (DateTime From, DateTime To) GetCurrentIsoWeek()
    {
        var today = DateTime.Today;
        var dayOfWeek = (int)today.DayOfWeek;           // Sun=0, Mon=1...Sat=6
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var from = today.AddDays(-daysFromMonday);       // most recent Monday 00:00
        return (from, from.AddDays(7));                  // next Monday 00:00
    }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.Service
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add GiselX.Service.Interface/IUploadCheckService.cs GiselX.Service/UploadCheckService.cs
git commit -m "feat: add UploadCheckService with monthly/ISO-week period resolution"
```

---

## Task 8: EmailSettings + EmailService

**Files:**
- Create: `GiselX.Service/EmailSettings.cs`
- Create: `GiselX.Service.Interface/IEmailService.cs`
- Create: `GiselX.Service/EmailService.cs`

- [ ] **Step 1: Create `GiselX.Service/EmailSettings.cs`**

```csharp
namespace GiselX.Service;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Create `GiselX.Service.Interface/IEmailService.cs`**

```csharp
namespace GiselX.Service.Interface;

public interface IEmailService
{
    Task SendReminderAsync(string toEmail, string companyName, DateTime deadline, bool isFinalReminder);
}
```

- [ ] **Step 3: Create `GiselX.Service/EmailService.cs`**

```csharp
using GiselX.Service.Interface;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GiselX.Service;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendReminderAsync(string toEmail, string companyName, DateTime deadline, bool isFinalReminder)
    {
        var daysUntil = (deadline.Date - DateTime.Today).Days;
        var subject = isFinalReminder
            ? $"[Action Required] Data upload deadline is today — {companyName}"
            : $"[Reminder] Data upload due in {daysUntil} days — {companyName}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(companyName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = BuildBody(companyName, deadline, isFinalReminder) };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
        await client.AuthenticateAsync(_settings.Username, _settings.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static string BuildBody(string companyName, DateTime deadline, bool isFinalReminder)
    {
        var intro = isFinalReminder
            ? "This is a final reminder that your data upload deadline is TODAY."
            : $"This is a reminder that your data upload deadline is on {deadline:dd MMMM yyyy}.";

        return $"""
            Dear {companyName},

            {intro}

            Please ensure the following data has been uploaded before the deadline:
              - Sales Transactions
              - Stock Data
              - Service Level Data (TransDist)

            If you have already completed your upload, please disregard this message.

            Regards,
            GiselX System
            """;
    }
}
```

- [ ] **Step 4: Build to verify**

```powershell
dotnet build GiselX.Service
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```powershell
git add GiselX.Service/EmailSettings.cs GiselX.Service.Interface/IEmailService.cs GiselX.Service/EmailService.cs
git commit -m "feat: add EmailService with MailKit SMTP implementation"
```

---

## Task 9: ICompanyRepository.GetAllWithContactEmailAsync

**Files:**
- Modify: `GiselX.Repository.Interface/ICompanyRepository.cs`
- Modify: `GiselX.Repository/CompanyRepository.cs`

- [ ] **Step 1: Add `GetAllWithContactEmailAsync` to `ICompanyRepository`**

Replace the contents of `GiselX.Repository.Interface/ICompanyRepository.cs`:

```csharp
using GiselX.Domain;
using GiselX.Domain.Common;

namespace GiselX.Repository.Interface;

public interface ICompanyRepository
{
    Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken);
    Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken);
    Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken);
    Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken);
    Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Update `CreateCompanyAsync` in `CompanyRepository.cs`**

Replace the `parameters` setup in `CreateCompanyAsync` to include the four new fields:

```csharp
public async Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken)
{
    const string query = "Company_Insert";

    var parameters = new DynamicParameters();
    parameters.Add("@Name", company.Name);
    parameters.Add("@Address", company.Address);
    parameters.Add("@ContactEmail", company.ContactEmail);
    parameters.Add("@DeadlineDayOfMonth", company.DeadlineDayOfMonth);
    parameters.Add("@DeadlineDaysOfWeek", company.DeadlineDaysOfWeek.HasValue ? (int)company.DeadlineDaysOfWeek.Value : (int?)null);
    parameters.Add("@ReminderLeadDays", company.ReminderLeadDays);

    await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
    var command = new CommandDefinition(
        query, parameters, _dbTransaction,
        cancellationToken: cancellationToken,
        commandType: CommandType.StoredProcedure);

    return await _dbConnection.QuerySingleAsync<Company>(command);
}
```

- [ ] **Step 3: Update `UpdateCompanyAsync` in `CompanyRepository.cs`**

```csharp
public async Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken)
{
    const string query = "Company_Update";

    var parameters = new DynamicParameters();
    parameters.Add("@Id", company.Id);
    parameters.Add("@Name", company.Name);
    parameters.Add("@Address", company.Address);
    parameters.Add("@ContactEmail", company.ContactEmail);
    parameters.Add("@DeadlineDayOfMonth", company.DeadlineDayOfMonth);
    parameters.Add("@DeadlineDaysOfWeek", company.DeadlineDaysOfWeek.HasValue ? (int)company.DeadlineDaysOfWeek.Value : (int?)null);
    parameters.Add("@ReminderLeadDays", company.ReminderLeadDays);

    await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
    var command = new CommandDefinition(
        query, parameters, _dbTransaction,
        cancellationToken: cancellationToken,
        commandType: CommandType.StoredProcedure);

    return await _dbConnection.QuerySingleAsync<Company>(command);
}
```

- [ ] **Step 4: Add `GetAllWithContactEmailAsync` implementation to `CompanyRepository.cs`**

Add the following method to `CompanyRepository`:

```csharp
public async Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken)
{
    const string query = "Company_SelectForReminder";

    await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
    var command = new CommandDefinition(
        query, null, _dbTransaction,
        cancellationToken: cancellationToken,
        commandType: CommandType.StoredProcedure);

    return await _dbConnection.QueryAsync<Company>(command);
}
```

- [ ] **Step 5: Build to verify**

```powershell
dotnet build GiselX.Repository
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```powershell
git add GiselX.Repository.Interface/ICompanyRepository.cs GiselX.Repository/CompanyRepository.cs
git commit -m "feat: update CompanyRepository for new deadline fields and GetAllWithContactEmailAsync"
```

---

## Task 10: ReminderJob

**Files:**
- Create: `GiselX.Service/ReminderJob.cs`

- [ ] **Step 1: Create `GiselX.Service/ReminderJob.cs`**

```csharp
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Interface;

namespace GiselX.Service;

public class ReminderJob
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUploadCheckService _uploadCheckService;
    private readonly IEmailService _emailService;

    public ReminderJob(
        ICompanyRepository companyRepository,
        IUploadCheckService uploadCheckService,
        IEmailService emailService)
    {
        _companyRepository = companyRepository;
        _uploadCheckService = uploadCheckService;
        _emailService = emailService;
    }

    public async Task ExecuteAsync()
    {
        var today = DateTime.Today;
        var companies = await _companyRepository.GetAllWithContactEmailAsync(CancellationToken.None);

        foreach (var company in companies)
        {
            foreach (var (deadlineDate, isFinalReminder) in ResolveTriggers(company, today))
            {
                var isWeekly = company.DeadlineDaysOfWeek.HasValue;
                var hasUploaded = await _uploadCheckService.HasUploadedThisPeriodAsync(company.Id, isWeekly);
                if (!hasUploaded)
                    await _emailService.SendReminderAsync(company.ContactEmail!, company.Name, deadlineDate, isFinalReminder);
            }
        }
    }

    private static IEnumerable<(DateTime DeadlineDate, bool IsFinalReminder)> ResolveTriggers(Company company, DateTime today)
    {
        if (company.DeadlineDayOfMonth.HasValue)
        {
            var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
            var day = Math.Min(company.DeadlineDayOfMonth.Value, daysInMonth);
            var deadline = new DateTime(today.Year, today.Month, day);

            if (today.Date == deadline.Date)
                yield return (deadline, true);
            else if (today.Date == deadline.Date.AddDays(-company.ReminderLeadDays))
                yield return (deadline, false);
        }
        else if (company.DeadlineDaysOfWeek.HasValue)
        {
            foreach (DayOfWeek dow in Enum.GetValues<DayOfWeek>())
            {
                var flag = (WeekDays)(1 << (int)dow);
                if (!company.DeadlineDaysOfWeek.Value.HasFlag(flag)) continue;

                // Find next occurrence of this day of week from today (0 = today if today matches)
                var daysUntil = ((int)dow - (int)today.DayOfWeek + 7) % 7;
                var nextOccurrence = today.AddDays(daysUntil);

                if (today.Date == nextOccurrence.Date)
                    yield return (nextOccurrence, true);
                else if (today.Date.AddDays(company.ReminderLeadDays) == nextOccurrence.Date)
                    yield return (nextOccurrence, false);
            }
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build GiselX.Service
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```powershell
git add GiselX.Service/ReminderJob.cs
git commit -m "feat: add ReminderJob with monthly/weekly deadline trigger and upload suppression logic"
```

---

## Task 11: HangfireAdminAuthFilter + permission constant

**Files:**
- Modify: `GiselX.Common/Constants/PermissionConstants.cs`
- Create: `GiselX.Web/HangfireAdminAuthFilter.cs`

- [ ] **Step 1: Add `Hangfire` class to `PermissionConstants.cs`**

Add the following nested class inside `PermissionConstants`, before the `GetAllPermissions()` method:

```csharp
public static class Hangfire
{
    public const string Dashboard = "Hangfire.Dashboard";
}
```

- [ ] **Step 2: Create `GiselX.Web/HangfireAdminAuthFilter.cs`**

```csharp
using GiselX.Common.Constants;
using Hangfire.Dashboard;

namespace GiselX.Web;

public class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
               && httpContext.User.HasClaim("permission", PermissionConstants.Hangfire.Dashboard);
    }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add GiselX.Common/Constants/PermissionConstants.cs GiselX.Web/HangfireAdminAuthFilter.cs
git commit -m "feat: add Hangfire.Dashboard permission and dashboard auth filter"
```

---

## Task 12: DI registrations

**Files:**
- Modify: `GiselX.Repository/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Register `IUploadCheckRepository` in `GiselX.Repository/ServiceCollectionExtensions.cs`**

Add one line after the existing repository registrations:

```csharp
services.AddScoped<IUploadCheckRepository, UploadCheckRepository>();
```

The complete file:

```csharp
using System.Data;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Repository;

public static class ServiceCollectionExtensions
{
    public static void AddGiselXRepository(this IServiceCollection services)
    {
        services.AddScoped<IDbConnection>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            return new SqlConnection(connectionString);
        });
        services.AddScoped<IDbTransaction>(sp =>
        {
            var dbConnection = sp.GetRequiredService<IDbConnection>();
            if (dbConnection.State != ConnectionState.Open)
                dbConnection.Open();
            return dbConnection.BeginTransaction();
        });

        services.AddScoped<IServiceLevelRepository, ServiceLevelRepository>();
        services.AddScoped<ISalesTransactionRepository, SalesTransactionRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IUploadCheckRepository, UploadCheckRepository>();
    }
}
```

- [ ] **Step 2: Register `IEmailService`, `IUploadCheckService`, and `ReminderJob` in `GiselX.Service/ServiceCollectionExtensions.cs`**

```csharp
using GiselX.Service.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Service;

public static class ServiceCollectionExtensions
{
    public static void AddGiselXService(this IServiceCollection services)
    {
        services.AddScoped<IServiceLevelService, ServiceLevelService>();
        services.AddScoped<ISalesTransactionService, SalesTransactionService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUploadCheckService, UploadCheckService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ReminderJob>();
    }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add GiselX.Repository/ServiceCollectionExtensions.cs GiselX.Service/ServiceCollectionExtensions.cs
git commit -m "feat: register UploadCheckRepository, UploadCheckService, EmailService, ReminderJob in DI"
```

---

## Task 13: Program.cs — Hangfire setup + appsettings

**Files:**
- Modify: `GiselX.Web/Program.cs`
- Modify: `GiselX.Web/appsettings.json`

- [ ] **Step 1: Add `Email` and `Hangfire` sections to `appsettings.json`**

Replace `GiselX.Web/appsettings.json` with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=<DB-SERVER>;Database=GiselXDb;User Id=<DB-USER>;Password=<REDACTED>;TrustServerCertificate=true;"
  },
  "Email": {
    "SmtpHost": "",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "",
    "Password": "",
    "FromAddress": "noreply@giselx.com",
    "FromName": "GiselX System"
  },
  "Hangfire": {
    "DailyJobHour": 8
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

> Fill in `SmtpHost`, `Username`, and `Password` with real values in `appsettings.Production.json` (not committed to source control) or environment variables.

- [ ] **Step 2: Update `Program.cs` — add Hangfire and EmailSettings registration**

After `builder.Services.AddControllersWithViews();`, add:

```csharp
builder.Services.Configure<GiselX.Service.EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
```

After `app.MapRazorPages().WithStaticAssets();`, add:

```csharp
app.MapHangfireDashboard("/hangfire", new Hangfire.Dashboard.DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthFilter() }
});

var dailyJobHour = app.Configuration.GetValue<int>("Hangfire:DailyJobHour", 8);
Hangfire.RecurringJob.AddOrUpdate<GiselX.Service.ReminderJob>(
    "daily-reminder",
    job => job.ExecuteAsync(),
    $"0 {dailyJobHour} * * *");
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build GiselX.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Run the app and verify Hangfire initializes**

```powershell
dotnet run --project GiselX.Web
```

Check the console output for lines like:
```
Starting Hangfire Server
Using job storage: 'SQL Server: <DB-SERVER>@GiselXDb'
```

Navigate to `https://localhost:<port>/hangfire` — you should be redirected to the login page (auth filter requires the `Hangfire.Dashboard` permission claim). After assigning that permission to an admin user and logging in, confirm the dashboard loads and shows the `daily-reminder` recurring job scheduled for `0 8 * * *`.

- [ ] **Step 5: Commit**

```powershell
git add GiselX.Web/Program.cs GiselX.Web/appsettings.json
git commit -m "feat: register Hangfire with SQL Server, EmailSettings, and schedule daily reminder job"
```

---

## Task 14: Admin UI — Company Create/Edit views

**Files:**
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Create.cshtml`
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml`

- [ ] **Step 1: Replace `Create.cshtml`**

```cshtml
@model GiselX.Service.Dto.CompanyDto

@{
    ViewData["Title"] = "Add Company";
    Layout = "_ContentNavbarLayout";
}

<div class="card">
    <div class="card-header d-flex align-items-center justify-content-between">
        <h5 class="mb-0">Add Company</h5>
        <small class="text-muted float-end">Company Registration</small>
    </div>
    <div class="card-body">
        @if (ViewBag.StatusMessage != null)
        {
            <div class="alert alert-success">@ViewBag.StatusMessage</div>
        }
        <form asp-action="Create" method="post">
            <div asp-validation-summary="All" class="text-danger mb-3"></div>
            <div class="row mb-4">
                <label asp-for="Name" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-10">
                    <input asp-for="Name" class="form-control" />
                    <span asp-validation-for="Name" class="text-danger"></span>
                </div>
            </div>
            <div class="row mb-4">
                <label asp-for="Address" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-10">
                    <input asp-for="Address" class="form-control" />
                    <span asp-validation-for="Address" class="text-danger"></span>
                </div>
            </div>
            <div class="row mb-4">
                <label asp-for="ContactEmail" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-10">
                    <input asp-for="ContactEmail" class="form-control" type="email" />
                    <span asp-validation-for="ContactEmail" class="text-danger"></span>
                    <small class="text-muted">Leave blank to disable reminders for this company.</small>
                </div>
            </div>
            <div class="row mb-4">
                <label class="col-sm-2 col-form-label">Deadline Type</label>
                <div class="col-sm-10 pt-2">
                    <div class="form-check form-check-inline">
                        <input class="form-check-input" type="radio" name="deadlineType" id="deadlineTypeNone" value="none" checked />
                        <label class="form-check-label" for="deadlineTypeNone">None</label>
                    </div>
                    <div class="form-check form-check-inline">
                        <input class="form-check-input" type="radio" name="deadlineType" id="deadlineTypeMonthly" value="monthly" />
                        <label class="form-check-label" for="deadlineTypeMonthly">Day of Month</label>
                    </div>
                    <div class="form-check form-check-inline">
                        <input class="form-check-input" type="radio" name="deadlineType" id="deadlineTypeWeekly" value="weekly" />
                        <label class="form-check-label" for="deadlineTypeWeekly">Days of Week</label>
                    </div>
                </div>
            </div>
            <div class="row mb-4 d-none" id="sectionMonthly">
                <label asp-for="DeadlineDayOfMonth" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-4">
                    <input asp-for="DeadlineDayOfMonth" class="form-control" type="number" min="1" max="28" />
                    <span asp-validation-for="DeadlineDayOfMonth" class="text-danger"></span>
                    <small class="text-muted">Day 1–28 of each month.</small>
                </div>
            </div>
            <div class="row mb-4 d-none" id="sectionWeekly">
                <label class="col-sm-2 col-form-label">Days of Week</label>
                <div class="col-sm-10 pt-2">
                    @foreach (var (label, value) in new[] { ("Monday", 2), ("Tuesday", 4), ("Wednesday", 8), ("Thursday", 16), ("Friday", 32), ("Saturday", 64), ("Sunday", 1) })
                    {
                        <div class="form-check form-check-inline">
                            <input class="form-check-input weekday-check" type="checkbox" id="wd_@value" data-value="@value" />
                            <label class="form-check-label" for="wd_@value">@label</label>
                        </div>
                    }
                </div>
                <input type="hidden" asp-for="DeadlineDaysOfWeek" id="DeadlineDaysOfWeekHidden" />
            </div>
            <div class="row mb-4">
                <label asp-for="ReminderLeadDays" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-4">
                    <input asp-for="ReminderLeadDays" class="form-control" type="number" min="0" max="30" />
                    <span asp-validation-for="ReminderLeadDays" class="text-danger"></span>
                    <small class="text-muted">Days before deadline to send the early warning email.</small>
                </div>
            </div>
            <div class="row justify-content-end">
                <div class="col-sm-10">
                    <button type="submit" class="btn btn-primary">Create Company</button>
                    <a asp-action="Index" class="btn btn-secondary ms-2">Cancel</a>
                </div>
            </div>
        </form>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script>
        (function () {
            const radios = document.querySelectorAll('input[name="deadlineType"]');
            const sectionMonthly = document.getElementById('sectionMonthly');
            const sectionWeekly = document.getElementById('sectionWeekly');
            const monthlyInput = document.getElementById('DeadlineDayOfMonth');
            const weeklyHidden = document.getElementById('DeadlineDaysOfWeekHidden');

            function toggleSections() {
                const val = document.querySelector('input[name="deadlineType"]:checked').value;
                sectionMonthly.classList.toggle('d-none', val !== 'monthly');
                sectionWeekly.classList.toggle('d-none', val !== 'weekly');
                if (val !== 'monthly') monthlyInput.value = '';
                if (val !== 'weekly') weeklyHidden.value = '';
            }

            radios.forEach(r => r.addEventListener('change', toggleSections));

            document.querySelectorAll('.weekday-check').forEach(cb => {
                cb.addEventListener('change', function () {
                    const total = [...document.querySelectorAll('.weekday-check:checked')]
                        .reduce((sum, el) => sum + parseInt(el.dataset.value), 0);
                    weeklyHidden.value = total > 0 ? total : '';
                });
            });
        })();
    </script>
}
```

- [ ] **Step 2: Replace `Edit.cshtml`**

```cshtml
@model GiselX.Service.Dto.CompanyDto

@{
    ViewData["Title"] = "Edit Company";
    Layout = "_ContentNavbarLayout";
}

<div class="card">
    <div class="card-header d-flex align-items-center justify-content-between">
        <h5 class="mb-0">Edit Company</h5>
        <small class="text-muted float-end">Company Update</small>
    </div>
    <div class="card-body">
        @if (ViewBag.StatusMessage != null)
        {
            <div class="alert alert-success">@ViewBag.StatusMessage</div>
        }
        <form asp-action="Edit" method="post">
            <input type="hidden" asp-for="Id" />
            <div asp-validation-summary="All" class="text-danger mb-3"></div>
            <div class="row mb-4">
                <label asp-for="Name" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-10">
                    <input asp-for="Name" class="form-control" />
                    <span asp-validation-for="Name" class="text-danger"></span>
                </div>
            </div>
            <div class="row mb-4">
                <label asp-for="Address" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-10">
                    <input asp-for="Address" class="form-control" />
                    <span asp-validation-for="Address" class="text-danger"></span>
                </div>
            </div>
            <div class="row mb-4">
                <label asp-for="ContactEmail" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-10">
                    <input asp-for="ContactEmail" class="form-control" type="email" />
                    <span asp-validation-for="ContactEmail" class="text-danger"></span>
                    <small class="text-muted">Leave blank to disable reminders for this company.</small>
                </div>
            </div>
            <div class="row mb-4">
                <label class="col-sm-2 col-form-label">Deadline Type</label>
                <div class="col-sm-10 pt-2">
                    <div class="form-check form-check-inline">
                        <input class="form-check-input" type="radio" name="deadlineType" id="deadlineTypeNone" value="none" />
                        <label class="form-check-label" for="deadlineTypeNone">None</label>
                    </div>
                    <div class="form-check form-check-inline">
                        <input class="form-check-input" type="radio" name="deadlineType" id="deadlineTypeMonthly" value="monthly" />
                        <label class="form-check-label" for="deadlineTypeMonthly">Day of Month</label>
                    </div>
                    <div class="form-check form-check-inline">
                        <input class="form-check-input" type="radio" name="deadlineType" id="deadlineTypeWeekly" value="weekly" />
                        <label class="form-check-label" for="deadlineTypeWeekly">Days of Week</label>
                    </div>
                </div>
            </div>
            <div class="row mb-4 d-none" id="sectionMonthly">
                <label asp-for="DeadlineDayOfMonth" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-4">
                    <input asp-for="DeadlineDayOfMonth" class="form-control" type="number" min="1" max="28" />
                    <span asp-validation-for="DeadlineDayOfMonth" class="text-danger"></span>
                    <small class="text-muted">Day 1–28 of each month.</small>
                </div>
            </div>
            <div class="row mb-4 d-none" id="sectionWeekly">
                <label class="col-sm-2 col-form-label">Days of Week</label>
                <div class="col-sm-10 pt-2">
                    @foreach (var (label, value) in new[] { ("Monday", 2), ("Tuesday", 4), ("Wednesday", 8), ("Thursday", 16), ("Friday", 32), ("Saturday", 64), ("Sunday", 1) })
                    {
                        <div class="form-check form-check-inline">
                            <input class="form-check-input weekday-check" type="checkbox" id="wd_@value" data-value="@value" />
                            <label class="form-check-label" for="wd_@value">@label</label>
                        </div>
                    }
                </div>
                <input type="hidden" asp-for="DeadlineDaysOfWeek" id="DeadlineDaysOfWeekHidden" />
            </div>
            <div class="row mb-4">
                <label asp-for="ReminderLeadDays" class="col-sm-2 col-form-label"></label>
                <div class="col-sm-4">
                    <input asp-for="ReminderLeadDays" class="form-control" type="number" min="0" max="30" />
                    <span asp-validation-for="ReminderLeadDays" class="text-danger"></span>
                    <small class="text-muted">Days before deadline to send the early warning email.</small>
                </div>
            </div>
            <div class="row justify-content-end">
                <div class="col-sm-10">
                    <button type="submit" class="btn btn-primary">Update Company</button>
                    <a asp-action="Index" class="btn btn-secondary ms-2">Cancel</a>
                </div>
            </div>
        </form>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script>
        (function () {
            const radios = document.querySelectorAll('input[name="deadlineType"]');
            const sectionMonthly = document.getElementById('sectionMonthly');
            const sectionWeekly = document.getElementById('sectionWeekly');
            const monthlyInput = document.getElementById('DeadlineDayOfMonth');
            const weeklyHidden = document.getElementById('DeadlineDaysOfWeekHidden');
            const existingDayOfMonth = @(Model.DeadlineDayOfMonth.HasValue ? Model.DeadlineDayOfMonth.Value.ToString() : "null");
            const existingDaysOfWeek = @(Model.DeadlineDaysOfWeek.HasValue ? Model.DeadlineDaysOfWeek.Value.ToString() : "null");

            function toggleSections() {
                const val = document.querySelector('input[name="deadlineType"]:checked').value;
                sectionMonthly.classList.toggle('d-none', val !== 'monthly');
                sectionWeekly.classList.toggle('d-none', val !== 'weekly');
                if (val !== 'monthly') monthlyInput.value = '';
                if (val !== 'weekly') weeklyHidden.value = '';
            }

            function setInitialState() {
                if (existingDayOfMonth) {
                    document.getElementById('deadlineTypeMonthly').checked = true;
                } else if (existingDaysOfWeek) {
                    document.getElementById('deadlineTypeWeekly').checked = true;
                    document.querySelectorAll('.weekday-check').forEach(cb => {
                        if (existingDaysOfWeek & parseInt(cb.dataset.value)) cb.checked = true;
                    });
                    weeklyHidden.value = existingDaysOfWeek;
                } else {
                    document.getElementById('deadlineTypeNone').checked = true;
                }
                toggleSections();
            }

            radios.forEach(r => r.addEventListener('change', toggleSections));

            document.querySelectorAll('.weekday-check').forEach(cb => {
                cb.addEventListener('change', function () {
                    const total = [...document.querySelectorAll('.weekday-check:checked')]
                        .reduce((sum, el) => sum + parseInt(el.dataset.value), 0);
                    weeklyHidden.value = total > 0 ? total : '';
                });
            });

            setInitialState();
        })();
    </script>
}
```

- [ ] **Step 3: Build and run the app**

```powershell
dotnet build GiselX.sln
dotnet run --project GiselX.Web
```

- [ ] **Step 4: Manual smoke test**

1. Navigate to `Admin/Companies/Create`
2. Verify the "Deadline Type" radios show/hide the correct input sections
3. Select "Day of Month", enter `10`, set `ReminderLeadDays` to `3`, enter a `ContactEmail`, click **Create Company**
4. Navigate to Edit for the new company — confirm all four new fields pre-populate correctly (Day of Month = 10, Lead Days = 3, Monthly radio selected)
5. Switch to "Days of Week", check Monday + Thursday → verify the hidden input value shows `18` (2 + 16)
6. Click **Update Company** and confirm the database row reflects the bitmask value `18`
7. In the Hangfire dashboard (`/hangfire`), click **Trigger now** on the `daily-reminder` job — confirm it runs without error in the job history tab

- [ ] **Step 5: Commit**

```powershell
git add GiselX.Web/Areas/Admin/Views/Companies/
git commit -m "feat: add deadline and reminder fields to Company Create/Edit views"
```
