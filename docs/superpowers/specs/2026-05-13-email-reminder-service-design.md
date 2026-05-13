# Email Reminder Service — Design Spec

**Date:** 2026-05-13  
**Author:** Chaidir Ali Assegaf  
**Status:** Approved

---

## Overview

A background email reminder service that notifies distributors/companies when they have not uploaded their required monthly or weekly data before a configured deadline. The service runs inside the existing `GiselX.Web` ASP.NET Core application using **Hangfire** for job scheduling and **MailKit** for SMTP email delivery.

A reminder is suppressed automatically once all three required tables (`SalesTransaction`, `Stock`, `ServiceLevel`) contain at least one row for that company within the current period.

---

## Goals

- Send an **early warning** email N days before the deadline (configurable per company).
- Send a **final reminder** email on the deadline day itself.
- Skip sending if the company has already uploaded data for the current period.
- Retry automatically on SMTP failure without manual intervention.
- Allow admins to configure deadlines and contact emails per company through the existing UI.

---

## Non-Goals

- Does not support email delivery outside SMTP (no SendGrid, no SES).
- Does not track whether a reminder email was opened or clicked.
- Does not send reminders for past missed deadlines — only the current period.
- Does not support per-table partial upload status reporting.

---

## Data Model

### Changes to `Company` entity

Four new nullable/defaulted fields are added directly to the existing `Company` entity:

| Field | Type | Nullable | Default | Description |
|---|---|---|---|---|
| `ContactEmail` | `string` | Yes | `null` | Recipient of reminder emails. If null, company is skipped. |
| `DeadlineDayOfMonth` | `int` | Yes | `null` | Day 1–28 of each month. Set this OR `DeadlineDaysOfWeek`, not both. |
| `DeadlineDaysOfWeek` | `int` | Yes | `null` | Bitmask of `WeekDays` flags enum. |
| `ReminderLeadDays` | `int` | No | `3` | Days before deadline to send the early warning. |

A company with both `DeadlineDayOfMonth` and `DeadlineDaysOfWeek` set to `null` is considered unconfigured and is skipped by the job.

### `WeekDays` Flags Enum

Located in `GiselX.Domain/WeekDays.cs`. Powers of 2 allow multiple days to be encoded in a single `int` column.

```csharp
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

**Example:** Monday + Thursday = `2 + 16 = 18` stored in the database.

### EF Core Migration

A new migration adds the four columns to the `Company` table in SQL Server. `ReminderLeadDays` gets a default value of `3`.

---

## Upload Check Logic

### Period resolution

| Deadline type | "Current period" date range |
|---|---|
| Monthly (`DeadlineDayOfMonth` set) | First day of current month → first day of next month |
| Weekly (`DeadlineDaysOfWeek` set) | Most recent Monday 00:00 → next Monday 00:00 (ISO week) |

### Check

All three tables must have at least one row satisfying:
- `CompanyId == company.Id`
- `CreatedDate >= periodStart AND CreatedDate < periodEnd`

Tables checked: `SalesTransaction`, `Stock`, `ServiceLevel`.

Returns `true` (deadline met) only when all three pass.

---

## Reminder Trigger Logic

The job runs daily at **08:00**. For each company with `ContactEmail` set:

### Monthly deadline

```
deadlineDate = new DateTime(today.Year, today.Month, DeadlineDayOfMonth)
             → clamped to last day of month if day > days in month

if today == deadlineDate                    → send final reminder
if today == deadlineDate - ReminderLeadDays → send early warning
```

### Weekly deadline

For each day set in the `DeadlineDaysOfWeek` bitmask:

```
nextOccurrence = next date (from today inclusive) matching that DayOfWeek

if today == nextOccurrence                    → send final reminder
if today == nextOccurrence - ReminderLeadDays → send early warning
```

### Suppression

If `HasUploadedThisPeriodAsync` returns `true` for a company, no email is sent regardless of whether today is a trigger day.

---

## Components

### `IUploadCheckRepository` / `UploadCheckRepository`

**Project:** `GiselX.Repository.Interface` / `GiselX.Repository`  
**Pattern:** Dapper (`IDbConnection`) — consistent with existing repositories.

```csharp
public interface IUploadCheckRepository
{
    Task<bool> HasDataForPeriodAsync(int companyId, DateTime from, DateTime to);
}
```

Runs three separate `SELECT 1 WHERE EXISTS(...)` queries against `SalesTransaction`, `Stock`, and `ServiceLevel`. Returns `true` only if all three return a result.

### `IUploadCheckService` / `UploadCheckService`

**Project:** `GiselX.Service.Interface` / `GiselX.Service`

```csharp
public interface IUploadCheckService
{
    Task<bool> HasUploadedThisPeriodAsync(int companyId, bool isWeekly);
}
```

Resolves the date range based on `isWeekly`, then delegates to `IUploadCheckRepository`.

### `EmailSettings`

**Project:** `GiselX.Service`  
Typed configuration POCO bound to the `"Email"` section of `appsettings.json`.

```csharp
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

### `IEmailService` / `EmailService`

**Project:** `GiselX.Service.Interface` / `GiselX.Service`  
**Dependency:** `MailKit` NuGet package.

```csharp
public interface IEmailService
{
    Task SendReminderAsync(string toEmail, string companyName, DateTime deadline, bool isFinalReminder);
}
```

Builds a MimeMessage and sends it via `MailKit.Net.Smtp.SmtpClient`. Subject lines:

- Early warning: `[Reminder] Data upload due in {N} days — {companyName}`
- Final reminder: `[Action Required] Data upload deadline is today — {companyName}`

Body lists the three tables the company needs to upload and the deadline date.

### `ReminderJob`

**Project:** `GiselX.Service`  
**Lifetime:** Scoped (Hangfire creates a DI scope per execution).

```csharp
public class ReminderJob
{
    public async Task ExecuteAsync();
}
```

Responsibilities:
1. Load all `Company` records where `ContactEmail IS NOT NULL`.
2. For each company, determine whether today is a reminder trigger day (early or final).
3. If triggered, call `IUploadCheckService.HasUploadedThisPeriodAsync`.
4. If not uploaded, call `IEmailService.SendReminderAsync`.

### `HangfireAdminAuthFilter`

**Project:** `GiselX.Web`  
Implements `IDashboardAuthorizationFilter`. Restricts access to `/hangfire` dashboard to users in the `Admin` role only.

---

## Configuration

### `appsettings.json` additions

```json
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
}
```

Sensitive values (`Username`, `Password`) should be overridden via environment variables or `appsettings.Production.json` (not committed to source control).

---

## Hangfire Registration

Changes to `GiselX.Web/Program.cs`:

```csharp
// Services
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<ReminderJob>();

// Middleware (after app.Build())
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthFilter() }
});

RecurringJob.AddOrUpdate<ReminderJob>(
    "daily-reminder",
    job => job.ExecuteAsync(),
    $"0 {dailyJobHour} * * *");
```

Hangfire uses the existing `DefaultConnection` SQL Server string and creates a `HangFire` schema in the same database — no second database needed.

**NuGet packages added to `GiselX.Web`:**
- `Hangfire.AspNetCore`
- `Hangfire.SqlServer`

**NuGet packages added to `GiselX.Service`:**
- `MailKit`

---

## Admin UI Changes

The existing `CompaniesController` Create and Edit forms are extended with four new fields:

- **Contact Email** — text input, optional
- **Deadline Type** — radio toggle: "Day of Month" / "Days of Week"
- **Day of Month** — number input (1–28), visible when "Day of Month" is selected
- **Days of Week** — checkboxes (Mon–Sun), visible when "Days of Week" is selected  
- **Reminder Lead Days** — number input, default 3

Client-side JavaScript shows/hides the relevant input based on the deadline type radio selection.

---

## New File List

```
GiselX.Domain/
  WeekDays.cs

GiselX.Repository.Interface/
  IUploadCheckRepository.cs

GiselX.Repository/
  UploadCheckRepository.cs

GiselX.Service.Interface/
  IEmailService.cs
  IUploadCheckService.cs

GiselX.Service/
  EmailSettings.cs
  EmailService.cs
  UploadCheckService.cs
  ReminderJob.cs

GiselX.Web/
  HangfireAdminAuthFilter.cs
  EF migration: Add deadline + contact fields to Company
  Update: Program.cs
  Update: GiselX.Web.csproj (Hangfire packages)
  Update: GiselX.Service.csproj (MailKit package)
  Update: CompaniesController.cs
  Update: Views/Companies/Create.cshtml
  Update: Views/Companies/Edit.cshtml
  Update: GiselXDbContext / GiselXDbContextSetup (Company config)
```

---

## Edge Cases

| Scenario | Behaviour |
|---|---|
| `DeadlineDayOfMonth = 31` in February | Clamped to 28 (or 29 on leap year) |
| Both deadline fields are null | Company skipped — no email sent |
| SMTP server unreachable | Hangfire retries up to 10 times with exponential backoff |
| Company has no users but has `ContactEmail` | Email still sent — `ContactEmail` is independent of user accounts |
| `ReminderLeadDays = 0` | Only the final reminder on deadline day is sent (early == final) |
| Data uploaded between early warning and deadline day | Final reminder suppressed — upload check runs fresh each day |
