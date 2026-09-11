# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Everything runs from the repo root unless noted. Target framework is **net10.0** (the README's ".NET 9.0" is stale).

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

EF Core migrations (design-time project is `GiselX.Web`):

```bash
dotnet ef migrations add <Name> --project GiselX.Web
dotnet ef database update --project GiselX.Web
```

The app calls `Database.MigrateAsync()` on every startup (inside `SeedFirstAdminAsync` in `Program.cs`), so a local run applies pending migrations automatically.

## Architecture

Layered solution; dependencies flow Web → Service → Repository → Domain, with interface projects (`*.Interface`) between the concrete layers and `GiselX.Service.Dto` / `GiselX.Mapper` (Mapperly, `MapperlyMapper`) crossing them.

### Two data-access stacks coexist — this is the single most important thing to know

1. **EF Core** (`GiselX.Web/Context/GiselXDbContext.cs`) owns Identity and is the **schema authority**: every business table has a `DbSet` and `OnModelCreating` configuration, and migrations live in `GiselX.Web/Context/Migrations`. Identity tables are mapped into the `identity` schema.
2. **Dapper + raw SQL** (`GiselX.Repository/*Repository.cs`) is what actually reads and writes business data at runtime — plus `SqlBulkCopy` for uploads. Repositories never touch `GiselXDbContext`.

So: change a domain entity → update the `DbSet`/`OnModelCreating` config → add an EF migration → update the hand-written SQL in the repository. The EF model exists largely so migrations and model-shape tests have a contract to assert against.

### The ambient connection/transaction

`AddGiselXRepository()` registers a scoped `IDbConnection` **and a scoped `IDbTransaction`** — resolving the transaction opens the connection and calls `BeginTransaction()`. Every repository in a request shares that one transaction. Services (not repositories) are responsible for calling `transaction.Commit()`; an uncommitted scope rolls back when disposed. When adding a write path, follow the existing services (e.g. `StockService`, `SelloutUploadService`) and commit explicitly.

`PowerBiReportMenuConnectionFactory` deliberately opens its **own** connection outside this ambient transaction — the menu view component runs on every page render and must not enlist in request transactions.

### Multi-tenancy and authorization

- `AppIdentityUser.CompanyId` is a required FK; nearly all business data is company-scoped, and controllers pass `user.CompanyId` down into services.
- Role `Administrator` sees across companies; role `Distributor` is restricted. Checks appear as `User.IsInRole("Administrator")` in controllers and via `UserClaimDto.IsAdministrator` in services (`UserClaimDto` is built per-request from claims in `Program.cs`).
- Permissions are string constants in `GiselX.Common/Constants/PermissionConstants.cs`. `Program.cs` reflects over that class and registers one authorization policy per constant, so **adding a nested constant is all that's needed** to make `[Authorize(PermissionConstants.Group.Action)]` work; role permissions are stored as `"permission"` role claims.

### Upload pipelines (Stock, Sellout/SalesTransaction)

The two upload flows mirror each other and are the bulk of the business logic:

1. **Workbook inspection/parsing** — `Xlsx*WorkbookInspector`, `*WorkbookParser`, `*HeaderNormalizer`, `*CellValueConverter` (ClosedXML).
2. **Template mapping** — a company's distributor workbook columns are mapped onto canonical fields (`SelloutCanonicalFieldCatalog`, `StockCanonicalFieldCatalog`, `DistributorTemplateMapping`/`StockTemplateMapping` + their `*Column` children). Mappings are resolved per company.
3. **Item mapping** (optional per company, `Company.ItemMappingEnabled`) — `ItemMappingResolver` translates distributor item ids/names to GMK canonical ones.
4. **Preflight → Upload** — `PreflightAsync` analyses and reports without writing; `UploadAsync` requires `confirmed: true`, writes a `*UploadBatch` audit row plus `*Staging` rows, then promotes to the final `Stock`/`SalesTransaction` table via `SqlBulkCopy`.

Batches record file SHA-256, row counts, ignored summary rows and validation errors as JSON — treat batch rows as immutable audit history.

### Background jobs & email

Hangfire (SQL Server storage, same connection string) is configured in `Program.cs`; the `daily-reminder` recurring job runs `ReminderJob.ExecuteAsync` at `Hangfire:DailyJobHour`. The dashboard is at `/hangfire` behind `HangfireAdminAuthFilter` (`Hangfire.Dashboard` permission). `EmailService`/`EmailSettings` bind the `Email` config section.

### Web layer shape

MVC controllers in `GiselX.Web/Controllers` (server-rendered views, DataTables-driven grids), JSON controllers under `Controllers/Api`, Identity + admin (Users/Roles) as Razor Pages under `Areas/Admin/Pages` and `Areas/Identity`. Services return `ServiceResponse` / `ServiceResponse<T>` (`Errors`, `ValidationErrors`, `StatusCode`, `IsSuccess`); controllers typically do `return StatusCode(response.StatusCode, response)`.

## Manual SQL deployment scripts

`GiselX.Web/DatabaseScripts/*.sql` are hand-written, idempotent installers that mirror specific EF migrations for production databases where `dotnet ef database update` isn't run. Each script must: run under `SET XACT_ABORT ON` in an explicit transaction, take an `sp_getapplock`, guard every object with existence checks, avoid any destructive DML against existing data, and `INSERT INTO dbo.__EFMigrationsHistory` (inside the transaction) so startup migration doesn't re-apply it.

`*ManualSqlScriptTests` assert these properties by string-matching the script files. If you add or change a migration that has a companion script, update both — the tests will fail otherwise.

## Test conventions

- xUnit, no mocking framework by default; tests are hand-rolled fakes/stubs.
- `*PersistenceModelTests` build a `GiselXDbContext` against a localdb connection string but only inspect `db.Model` — **no database is contacted**, so they run anywhere. Use this pattern to pin FK delete behaviors, nullability, column types and indexes.
- `*ManualSqlScriptTests` and `*MigrationTests` read files from disk, locating the repo root by walking up to `GiselX.sln`.
- Naming is `Subject_verb_expected_outcome` in snake case after the first word.

## Configuration

`appsettings.json` sections: `ConnectionStrings:DefaultConnection` (SQL Server; despite the README, SQLite is not wired up in `Program.cs`), `Email`, `Hangfire:DailyJobHour`, `StockUpload`/`SelloutUpload` (`MaxFileSizeBytes`), `AdminSeed`.

`AdminSeed` bootstraps a company, an `Administrator` role holding every permission, and a confirmed admin user — but only when the user table is empty, and only if a password is supplied out-of-band (user-secrets locally, `AdminSeed__Password` in production). A blank password logs a warning and skips seeding.

## Feature documentation

Design specs live in `docs/superpowers/specs/` and implementation plans in `docs/superpowers/plans/`, named `YYYY-MM-DD-<feature>`. Read the matching spec before modifying an upload pipeline, template-mapping, or Power BI feature — they record the agreed schema contracts and deployment behavior.
