# Task 9: Isolate dynamic menu reads from request transactions

Date: 2026-08-22

Base: `4fbfd57ab050c791c0945ca270a1e69486980f6c`

## Root cause evidence

`AddGiselXRepository` registered one scoped `IDbConnection` and a scoped
`IDbTransaction` factory which opens that connection and calls `BeginTransaction()`.
`PowerBiReportMenuRepository` also received the scoped `IDbConnection`. Its Dapper
command correctly omitted an explicit transaction, but that did not detach it from the
physical connection's pending local transaction. The view component caught the resulting
exception and rendered an empty menu.

The provider behavior was reproduced directly against a temporary SQL Server LocalDB
instance with Microsoft.Data.SqlClient 7.0.1. A `SqlConnection` was opened, a local
transaction was begun, and an async `SqlCommand` was executed on that connection without
assigning `SqlCommand.Transaction`. SqlClient threw:

> BeginExecuteReader requires the command to have a transaction when the connection
> assigned to the command is in a pending local transaction. The Transaction property
> of the command has not been initialized.

The temporary evidence test was removed after capture. The temporary `GiselXTask9`
LocalDB instance was stopped and deleted.

## TDD evidence

The permanent regression models that verified SqlClient contract behavior while running
the actual repository, menu service, view component, Identity company lookup, and
repository DI registrations. It begins the request-scope transaction before layout menu
rendering.

RED against `4fbfd57`:

- Command:
  `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~PowerBiReportMenuViewComponentTests.InvokeAsync_does_not_use_the_request_connection_with_a_pending_local_transaction --logger "console;verbosity=normal"`
- Expected zero transaction conflicts; actual conflict count was one. The component
  returned an empty model after the test connection raised the pending-local-transaction
  rejection.

A second RED covered diagnostic logging:

- Command:
  `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~PowerBiReportMenuViewComponentTests.InvokeAsync_returns_an_empty_menu_when_the_service_throws --logger "console;verbosity=minimal"`
- The warning level was emitted, but the caught exception supplied to the logger was
  `null` instead of the thrown exception.

GREEN behavior now proves:

- the active request connection is not queried by layout rendering;
- the dedicated connection is created lazily, opened within `InvokeAsync`'s protected
  call path, and asynchronously disposed after open, query, and cancellation failures;
- the exact request cancellation token reaches both `OpenAsync` and Dapper command
  execution;
- genuine request cancellation is rethrown;
- open and query errors render an empty menu and log the caught exception at warning
  level;
- the query retains `CompanyId = @CompanyId AND IsActive = 1`,
  `ORDER BY DisplayOrder, Name`, no explicit transaction, and the company parameter;
- company derivation remains inside `PowerBiReportMenuService` through the authenticated
  Identity user.

## Design rationale

A named `IPowerBiReportMenuConnectionFactory` was chosen over a generic
`Func<DbConnection>`, resolving a child DI scope, or reusing the request transaction.
The named boundary cannot collide with unrelated connection delegates, avoids service
location in the repository, and makes the menu's non-transactional connection ownership
explicit. It is also smaller than introducing a session abstraction. The current
Microsoft.Data.SqlClient package does not expose a provider-specific `DbDataSource` for
this use, so the factory returns the BCL `DbConnection` abstraction and constructs a new
`SqlConnection` for each call.

Factory and repository construction perform no database I/O. `CreateConnection()` only
constructs a closed `SqlConnection`; `PowerBiReportMenuRepository` calls `OpenAsync`
inside `ListActiveByCompanyAsync`, uses `await using`, and does not start or enlist in a
transaction. The existing scoped `IDbConnection`/`IDbTransaction` registrations and all
administrator/viewer mutation repository behavior remain unchanged.

## Changed files

- `GiselX.Repository.Interface/IPowerBiReportMenuConnectionFactory.cs`
- `GiselX.Repository/PowerBiReportMenuConnectionFactory.cs`
- `GiselX.Repository/PowerBiReportMenuRepository.cs`
- `GiselX.Repository/ServiceCollectionExtensions.cs`
- `GiselX.Web/Controllers/PowerBiReportMenuViewComponent.cs`
- `GiselX.Tests/PowerBiReportMenuViewComponentTests.cs`
- `GiselX.Tests/PowerBiReportRepositoryContractTests.cs`
- `.superpowers/sdd/2026-08-21-dynamic-power-bi-report-menu/task-9-report.md`

## Final verification

- Focused repository/service/component tests:
  `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~PowerBiReportMenuViewComponentTests|FullyQualifiedName~PowerBiReportMenuServiceTests|FullyQualifiedName~PowerBiReportRepositoryContractTests"`
  - PASS: 34 passed, 0 skipped, 0 failed.
- Power BI feature filter:
  `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~PowerBi`
  - PASS: 110 passed, 0 skipped, 0 failed.
- Full solution:
  `dotnet test GiselX.sln --no-restore`
  - PASS: 307 passed, 2 skipped, 0 failed, 309 total.
  - The two skips are the pre-existing LocalDB/manual SQL integration checks.
- Build:
  `dotnet build GiselX.sln --no-restore`
  - PASS: 0 errors, 6 warnings.
- Format changed C# files:
  `dotnet format GiselX.sln --no-restore --include <all seven changed C# files>`
  - PASS; workspace-load warning only.
- Format verification:
  `dotnet format GiselX.sln --no-restore --verify-no-changes --include <all seven changed C# files>`
  - PASS; workspace-load warning only.
- `git diff --check`
  - PASS; no whitespace errors. Git emitted only line-ending normalization notices.

## Concerns and explicit non-changes

The six build warnings are the existing NuGet vulnerability notices for
`NuGet.Packaging`/`NuGet.Protocol` 6.12.1 and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11; this
task did not change dependencies. It also did not change branch topology, migrations,
schema, URL policy, menu authorization, company resolution, mutation transactions, or
commit behavior.
