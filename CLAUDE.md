# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Everything runs from the repo root unless noted. Target framework is **net10.0**, `LangVersion 14`. The README is stale on two points: it says .NET 9.0, and it claims SQLite support that `Program.cs` does not wire up (SQL Server only).

```bash
dotnet build                                  # build the solution
dotnet test                                   # run all tests (GiselX.Tests, xUnit)
dotnet test --filter FullyQualifiedName~StockUploadServiceTests          # one test class
dotnet test --filter "FullyQualifiedName~StockManualSqlScriptTests.Script_contains_transaction_and_rerun_guards"  # one test
dotnet run --project GiselX.Web               # run the app
```

Frontend assets (Materio Bootstrap template, gulp + webpack) live in `GiselX.Web`:

```bash
cd GiselX.Web
npm install
npm run build          # dev build into wwwroot
npm run build:prod     # production build
npm run watch
```

Template sources are `GiselX.Web/src` (scss/js/libs) and gulp compiles them into `wwwroot`. Hand-written page scripts (e.g. `power-bi-reports.js`, `site.js`) live directly in `wwwroot/js` and are referenced from views — they are not part of the gulp pipeline.

EF Core migrations (design-time project is `GiselX.Web`):

```bash
dotnet ef migrations add <Name> --project GiselX.Web
dotnet ef database update --project GiselX.Web
```

The app calls `Database.MigrateAsync()` on every startup (inside `SeedFirstAdminAsync` in `Program.cs`), so a local run applies pending migrations automatically.

**`.worktrees/` holds gitignored full checkouts of old feature branches.** A repo-wide `grep`/`find` will hit them and return stale duplicates of every file. Scope searches to the `GiselX.*` project directories.

## Architecture

Layered solution; dependencies flow Web → Service → Repository → Domain, with interface projects (`*.Interface`) between the concrete layers and `GiselX.Service.Dto` / `GiselX.Mapper` (Mapperly, `MapperlyMapper`) crossing them. New repositories and services are only reachable once registered by hand in `GiselX.Repository/ServiceCollectionExtensions.cs` (`AddGiselXRepository`) and `GiselX.Service/ServiceCollectionExtensions.cs` (`AddGiselXService`) — there is no assembly scanning.

### Three ways the database is reached — this is the single most important thing to know

1. **EF Core** (`GiselX.Web/Context/GiselXDbContext.cs`) owns Identity and is the **schema authority**: every business table has a `DbSet` and `OnModelCreating` configuration, and migrations live in `GiselX.Web/Context/Migrations`. Identity tables are mapped into the `identity` schema. No repository ever touches `GiselXDbContext`.
2. **Dapper against stored procedures** — `CompanyRepository`, `ServiceLevelRepository` and `SalesTransactionRepository` call procs (`Company_*`, `Gisel_*`, `GiselSales_*`) with `CommandType.StoredProcedure`, each preceded by `SET ARITHABORT ON`. The readable source of each proc is `GiselX.Web/StoredProcedures/*.sql`, but **nothing deploys those files** — a proc reaches a database only because its body was pasted into a migration's `migrationBuilder.Sql(...)` (Up *and* Down) as `CREATE OR ALTER PROCEDURE [dbo].[Name]`. Changing a proc means editing both the `.sql` file and a migration. `StoredProcedureDeploymentTests` scans the repositories for every proc name they call and fails if no migration creates it. Its `NotDeployedByMigrations` list holds the exceptions: the legacy `Gisel_*` Service Level procs, which predate the migrations, and repository methods no service calls. Wiring one of those methods back up means removing it from the list and adding a migration. The Stock page no longer uses procs: its grid, count and summary tiles are inline SQL that share one `RankedBatches` CTE (`StockSnapshotPeriod`), which keeps the latest snapshot per distributor inside the chosen month. Stock snapshots are positions, so never sum them across dates.
3. **Dapper against inline SQL** — every newer repository (uploads, template mappings, item mappings, Power BI, channel/salesman/business unit, `UploadCheckRepository`) uses raw SQL strings. `SqlBulkCopy` appears only in the legacy direct-upload path (`StockRepository.UploadAsync`, `SalesTransactionRepository.UploadAsync`, `ServiceLevelRepository.UploadAsync`); the staging upload pipelines do not use it.

So a schema change touches, in order: the `DbSet`/`OnModelCreating` config → an EF migration → the hand-written SQL in the repository → the stored-proc `.sql` file *and* its copy inside the migration, if the table has procs → the companion manual SQL script, if the migration has one. The EF model exists largely so migrations and model-shape tests have a contract to assert against.

### The ambient connection/transaction

`AddGiselXRepository()` registers a scoped `IDbConnection` **and a scoped `IDbTransaction`** — resolving the transaction opens the connection and calls `BeginTransaction()`. Every repository in a request shares that one transaction. Services (not repositories) are responsible for calling `transaction.Commit()`; an uncommitted scope rolls back when disposed. When adding a write path, follow the existing services (e.g. `StockService`, `SelloutUploadService`) and commit explicitly — including on the failure branches, where the audit batch must survive even though nothing was published.

`PowerBiReportMenuConnectionFactory` deliberately opens its **own** connection outside this ambient transaction — the menu view component runs on every page render and must not enlist in request transactions.

### Multi-tenancy and authorization

- `AppIdentityUser.CompanyId` is a required FK; nearly all business data is company-scoped, and controllers pass `user.CompanyId` down into services. A distributor with several locations is registered as one company per location; `Company.Location` is the free-text name of that location, carried by the `Company_*` procs and exposed as `CompanyLocation` in both Power BI views.
- Role `Administrator` sees across companies; role `Distributor` is restricted. Checks appear as `User.IsInRole("Administrator")` in controllers and via `UserClaimDto.IsAdministrator` in services (`UserClaimDto` is built per-request from claims in `Program.cs`). The repository convention for "any company" is to pass `CompanyId = 0` and filter with `(@CompanyId = 0 OR CompanyId = @CompanyId)`, as in `StockRepository` and both upload repositories. PT. GMK staff are Administrators filed under their own company, so a lookup scoped to `user.CompanyId` breaks every link the internal notification mail sends them.
- Permissions are string constants in `GiselX.Common/Constants/PermissionConstants.cs`. `Program.cs` reflects over that class and registers one authorization policy per constant, so **adding a nested constant is all that's needed** to make `[Authorize(PermissionConstants.Group.Action)]` work; role permissions are stored as `"permission"` role claims. A new permission carved out of an existing one also needs a backfill migration granting it to the roles that already held the old one (see `20260911165830_GrantMappingPermissionsToCompanyEditors`).

### Web layer shape

MVC controllers all live in `GiselX.Web/Controllers`, but roughly half carry `[Area("Admin")]` and render from `GiselX.Web/Areas/Admin/Views/<Controller>` — master data and mapping administration (Companies, BusinessUnits, Channels, Salesmen, ItemMappings, both TemplateMappings, PowerBiReportsAdmin, EmailTest). The rest are area-less and render from `GiselX.Web/Views` (Home, Stocks, SalesTransactions, ServiceLevels, PowerBiReports). Identity and user/role admin are Razor Pages under `Areas/Identity` and `Areas/Admin/Pages`. JSON endpoints backing the DataTables grids are under `Controllers/Api`.

Services return `ServiceResponse` / `ServiceResponse<T>` (`Message`, `Errors`, `ValidationErrors`, `StatusCode`, and `IsSuccess`, which is derived from the two error lists — not from `StatusCode`). API controllers typically do `return StatusCode(response.StatusCode, response)`; MVC controllers surface `TempData["SuccessMessage"]` / `TempData["ErrorMessage"]`.

### Upload pipelines (Stock, Sellout/SalesTransaction)

The two staging-based upload flows mirror each other and are the bulk of the business logic:

1. **Workbook reading** — `LegacyExcelWorkbookConverter` first converts `.xls` (detected by OLE2 signature, not by file name) into `.xlsx` via ExcelDataReader, so everything downstream is OOXML-only ClosedXML: `XlsxWorkbookInspector` (shared), `Xlsx{Stock,Sellout}WorkbookInspector`, `*WorkbookParser`, `WorkbookHeaderNormalizer`, `WorkbookDateTextParser`, `*CellValueConverter`.
2. **Template matching** — a mapping matches when its stored `HeaderSignature` equals the worksheet's normalized header row. A mapping that fails that is retried by resolving each of its columns to whichever column now carries that `SourceHeader`, which rescues a reordered or newly-padded worksheet; the rescue is discarded for any worksheet another mapping already matched exactly. Matched columns map onto canonical fields (`SelloutCanonicalFieldCatalog`, `StockCanonicalFieldCatalog`); mappings are resolved per company.
3. **Item mapping** (optional per company, `Company.ItemMappingEnabled`) — `ItemMappingResolver` loads the company's active mappings once and translates distributor item ids/names to GMK canonical ones through an index.
4. **Preflight → Upload** — `PreflightAsync` analyses and reports without writing, and returns the SHA-256 of the bytes it analysed. `UploadAsync` requires both explicit confirmation and that same hash quoted back as `confirmedSha256`; `UploadConfirmation.DescribeMismatch` refuses an upload whose bytes differ from the preview that was approved. The views carry the hash from preview to confirm and clear it when the file input changes. The file is still sent and parsed twice — that is deliberate.
5. **Writing** — `UploadAsync` writes an audit row plus `*Staging` rows, then promotes the valid ones into `Stock`/`SalesTransaction` with an `INSERT … SELECT` inside the ambient transaction.

The two pipelines differ in shape: a Stock upload is one `StockUploadBatch` for one snapshot date, while a Sellout upload is one `SelloutUploadGroup` fanned out into a `SelloutUploadBatch` per ISO week (a multi-week workbook needs `multiWeekConfirmed`, and publication replaces the **invoice-date range** of the valid rows, not whole ISO weeks). Both accept partial success — valid rows publish and the batch ends `CompletedWithWarnings` — except that Sellout refuses the whole file when invalid rows reach `SelloutUpload:MaxInvalidRowPercentage` (default 5; set it to 0 for the old all-or-nothing behaviour). Each Stock row also carries a `StockAreaId` and a Good/Bad `Condition`: areas are a per-company master (`StockArea`, edited at Admin → Companies → Stock areas), and a company with active areas must map an Area column in its Stock templates or the workbook is refused.

The staging pipelines stamp `Stock`/`SalesTransaction.CreatedDate` with `SYSUTCDATETIME()`, but the legacy Service Level path stamps `TransDist` with server-local `DateTime.Now`. Any query that buckets rows by upload time into calendar periods must convert the period boundary separately for each table; `UploadCheckService` passes the period in both forms for this reason.

Batches record file SHA-256, row counts, ignored summary rows and validation errors as JSON — treat batch rows as immutable audit history. The one sanctioned mutation is supersession: publishing a Sellout group deletes the company's published rows (legacy no-batch rows included) between the earliest and latest invoice date of its valid rows, marks a prior batch `Replaced` only when none of its rows remain, and deletes that batch's staging rows; several batches can therefore be live for one ISO week and the Sellout month view shows every live row. Invalid rows never widen the range. Publishes take a per-company `sp_getapplock`. Meanwhile publishing a Stock snapshot date replaces only the **areas** the upload carries (compared as `COALESCE(StockAreaId,0)`, so a company without areas still replaces the whole date) and marks a prior batch `Replaced` only when none of its rows remain. One company can therefore have several live Stock batches on one date, so every Stock reader (the Stock page, `GetPublishedAreasAsync`, `vw_GiselStock_Live`) ranks `(batch, area)` pairs per `(CompanyId, area)` rather than one batch per company. **The status enums are numbered differently** — `StockUploadBatchStatus.Replaced = 4` / `CompletedWithWarnings = 3`, but `SelloutUploadBatchStatus.Replaced = 3` / `CompletedWithWarnings = 4` — and cannot be aligned without rewriting stored history, so any SQL filtering on status must use each enum's own values (`UploadBatchStatusTests` pins this). Stock rows published before supersession existed may still be duplicated per date; `DatabaseScripts/Cleanup_DuplicateStockSnapshots.sql` removes them (preview by default) using the same per-(company, date, area) ranking, so it deletes only the rows of a superseded (batch, area) pair and never treats another area's live batch as a duplicate and is deliberately never run automatically.

`ServiceLevels` is the surviving **legacy** upload: the controller reads fixed cell positions with ClosedXML into DTOs and the service bulk-copies them. No preflight, staging, mapping or audit batch. Don't use it as the model for new work.

### Background jobs & email

Hangfire (SQL Server storage, same connection string) is configured in `Program.cs`; the `daily-reminder` recurring job runs `ReminderJob.ExecuteAsync` at `Hangfire:DailyJobHour`. Each company with a `ContactEmail` has either a `DeadlineDayOfMonth` or a `DeadlineDaysOfWeek` flags value; `ReminderJob` sends a lead-time reminder `ReminderLeadDays` before the deadline and a final one on the day, and `UploadCheckService` suppresses both when data already exists for the current month or ISO week. The dashboard is at `/hangfire` behind `HangfireAdminAuthFilter` (`Hangfire.Dashboard` permission). `EmailService` (MailKit) gets its settings from `EmailSettingsProvider`: the single `EmailSetting` database row (edited at Admin → Email Settings, `EmailSettings.Manage` permission) wins, and the `Email` config section is only the fallback for an environment nobody has configured. The stored SMTP password is encrypted with Data Protection (`IEmailSecretProtector`) and never sent back to the browser, so losing the data-protection keys makes it unreadable until re-entered.

A completed upload also mails both audiences. Each upload service calls `IUploadNotificationDispatcher.Enqueue` **after** its `transaction.Commit()` — never before, or the mail could describe rows that rolled back — and only on a published outcome, so a refused workbook notifies nobody. The Service layer depends on that interface rather than on Hangfire; `HangfireUploadNotificationDispatcher` in `GiselX.Web` is the implementation, and it swallows queueing failures because the upload has already succeeded by then. The job itself is `UploadNotificationService`, which addresses the distributor via `Company.ContactEmail` and internal PT. GMK staff via the `NotificationEmails` of the company's `BusinessUnit`, `Channel` and `Salesman` (merged and de-duplicated by `NotificationRecipients.Combine`), and returns without sending when the resolved `SmtpHost` is blank rather than letting Hangfire retry a misconfiguration ten times per upload.

`IUploadArchive` keeps the workbook itself under `UploadArchive:Path`, and **every one of its failure modes is silent by design** — no folder configured, a folder the app pool cannot write to, a file pruned since — because an upload whose rows are already valid must not be refused over a full disk. The cost is that a notification quietly arrives with no download link and looks like a bug. `IUploadArchive.CheckAsync` writes, reads back and deletes a probe file, and the Email Test screen (`/Admin/EmailTest`) runs it on every page load alongside the resolved links, so both requirements for a download link are visible without performing an upload. `UploadLinkReadiness` composes that report and deliberately builds its URLs through `UploadNotificationMessage` rather than its own copy — a check that disagrees with the thing it checks is worse than none.

**`Company.ContactEmail` and the `NotificationEmails` columns are `;`-delimited lists, not single addresses.** `NotificationRecipients` is the only place that splits, validates and normalises them — services validate with `FindInvalid`/`Normalize` before storing, and `ReminderJob` and `UploadNotificationService` both split with `Parse` before handing a collection to `IEmailService`. Never pass a stored value straight to MimeKit: `MailboxAddress` throws `ParseException` on a `;`, which used to kill a company's reminders silently.

`IStandaloneConnectionFactory` gives work that must not enlist in the scoped ambient transaction its own connection. `UploadNotificationRepository` uses it because its Hangfire scope stays alive across the SMTP exchange; `PowerBiReportMenuConnectionFactory` predates it and does the same thing for the menu view component, and the two could be folded together.

## Hand-written SQL

Anything written by hand rather than generated by EF must bracket the identity schema as `[identity].[AspNetRoleClaims]` — `identity` is the T-SQL `IDENTITY` keyword, and an unbracketed reference is a syntax error no test catches, because manual-script tests only string-match and migration SQL is never executed by a test. This has broken startup once already.

### Manual SQL deployment scripts

`GiselX.Web/DatabaseScripts/*.sql` are hand-written, idempotent installers that mirror specific EF migrations for production databases where `dotnet ef database update` isn't run. Each script must: run under `SET XACT_ABORT ON` in an explicit transaction, take an `sp_getapplock`, guard every object with existence checks, avoid any destructive DML against existing data, and `INSERT INTO dbo.__EFMigrationsHistory` (inside the transaction) so startup migration doesn't re-apply it.

Two files there are not migration mirrors and follow different rules: `Reporting_StockAndSellout.sql` (views + procs for Power BI that de-duplicate Stock; idempotent, re-run to update, deliberately outside the migration chain and `__EFMigrationsHistory` because the app never calls them) and `Cleanup_DuplicateStockSnapshots.sql` (a destructive one-off the operator runs by choice).

A script is one batch, and SQL Server compiles the whole batch before running any of it. A statement that references a column the same script adds to an *existing* table therefore fails with "Invalid column name" and nothing runs. Put such statements, like procedure bodies, inside `sp_executesql`. The only real check is to run every script in order, twice, against a database built up to the preceding migration.

`*ManualSqlScriptTests` assert these properties by string-matching the script files. If you add or change a migration that has a companion script, update both — the tests will fail otherwise.

## Test conventions

- xUnit, no mocking framework by default; tests are hand-rolled fakes/stubs. NPOI is referenced purely to build `.xls` fixtures.
- `*PersistenceModelTests` build a `GiselXDbContext` against a localdb connection string but only inspect `db.Model` — **no database is contacted**, so they run anywhere. Use this pattern to pin FK delete behaviors, nullability, column types and indexes.
- `*ManualSqlScriptTests` and `*MigrationTests` read files from disk, locating the repo root by walking up to `GiselX.sln`.
- Naming is `Subject_verb_expected_outcome` in snake case after the first word.
- Nothing in the suite executes migration SQL, stored procedures or repository queries, so schema, proc and query changes have to be verified by running them against a real SQL Server. Tests can only pin the SQL's *shape*, e.g. `UploadResultAdministratorScopeTests` and `StockSnapshotPeriodTests`.

## Configuration

`appsettings.json` sections: `ConnectionStrings:DefaultConnection` (SQL Server), `Email`, `Hangfire:DailyJobHour`, `StockUpload` (`MaxFileSizeBytes`), `SelloutUpload` (`MaxFileSizeBytes`, `MaxInvalidRowPercentage`), `UploadArchive:Path`, `AdminSeed`. SMTP settings in the database override the `Email` section (see above), which exists because a copy-paste deploy that replaced `appsettings.json` once took the connection string with it.

`AdminSeed` bootstraps a company, an `Administrator` role holding every permission, and a confirmed admin user — but only when the user table is empty, and only if a password is supplied out-of-band (user-secrets locally, `AdminSeed__Password` in production). A blank password logs a warning and skips seeding; `AdminSeed:Enabled=false` skips it and the startup migration along with it.

## Feature documentation

Design specs live in `docs/superpowers/specs/` and implementation plans in `docs/superpowers/plans/`, named `YYYY-MM-DD-<feature>`. Read the matching spec before modifying an upload pipeline, template-mapping, or Power BI feature — they record the agreed schema contracts and deployment behavior.

## Commit messages

History here is written to a consistent standard worth matching: a `feat:`/`fix:`/`refactor:`/`perf:`/`test:` subject in the imperative, then a body in prose paragraphs stating the behaviour that was wrong and why it mattered, what the change does instead, what it deliberately does *not* do, and how it was verified beyond the test suite — the suite frequently cannot cover these changes, so say so and say what was run instead. Read `git log` before writing one.
