# Final Power BI review fix report

Date: 2026-08-22

Base: `91cb1c4e547c5ce585623ffacef63b88e62f3cd0`

Scope: the four CODE findings from the final whole-feature review

## Verified root causes and fixes

### 1. Layout availability and menu transaction lifetime

The reviewer claim was confirmed through the actual DI graph. Resolving
`PowerBiReportMenuViewComponent` resolved `IPowerBiReportService`, whose
`PowerBiReportService` constructor required both `PowerBiReportRepository` and
`CompanyRepository` plus `IDbTransaction`. The scoped transaction factory opens the
shared SQL connection and calls `BeginTransaction()` during service activation, before
`InvokeAsync()` enters its exception boundary. The menu repository command also
explicitly received that transaction.

The fix separates the layout read into `IPowerBiReportMenuService` and
`IPowerBiReportMenuRepository`. Their constructors perform no database I/O; the menu
repository receives only the unopened scoped `IDbConnection`, and its Dapper command
has no explicit transaction. The menu service still derives `CompanyId` internally
from the authenticated Identity user, and the component and service forward the exact
request cancellation token. Request cancellation is still rethrown; ordinary identity,
open, and query failures log the fixed generic warning and render an empty menu.

The mutation service/repository retain their original scoped transaction and commit
behavior. The old transactional menu methods were removed from those mutation-oriented
interfaces so there is no alternate explicit-transaction menu path. Tenant and
`IsActive = 1` predicates are unchanged.

### 2. Keyed Save validation

The claim was confirmed: Save failures exposed only `List<string> Errors`, and the
controller added every service error under `ModelState[string.Empty]`.

`ServiceResponse` now supports structured `ServiceValidationError(Key, Message)`
values while retaining the existing string errors for compatibility. Save validation
uses stable DTO property keys for `CompanyId`, `Name`, `EmbedUrl`, and `DisplayOrder`.
Unknown-company errors use `CompanyId`; normalized URL length and URL-format errors use
`EmbedUrl`; both duplicate pre-checks and typed unique-index races use `Name`. The
controller maps structured errors directly to ModelState keys without parsing message
text, so the existing `asp-validation-for` elements display them. Non-structured errors
keep the existing model-summary fallback. Existing 400/404 handling, posted values,
company option reloads, audit assignment, repository mutation, and commit behavior are
unchanged.

### 3. Unfiltered Admin ordering

The claim was confirmed by a real in-memory SQL execution: Company ID 2 (`Zulu`) sorted
before Company ID 10 (`Alpha`). The Admin query now joins `Company` and orders by
`company.Name`, `report.DisplayOrder`, `report.Name`, and finally `report.Id` for a
deterministic tie. The optional company filter remains parameterized.

### 4. Viewer recovery

The viewer now includes a normal accessible **Retry loading report** button inside the
`#power-bi-viewer` fullscreen subtree. The validated, Razor-encoded server URL is stored
in `data-report-url`; retry shows the loading treatment, updates the live accessibility
status, and assigns only that URL back to `iframe.src`. It uses no `innerHTML`. The
permanent encoded new-tab fallback remains present. Retry is registered before the
Fullscreen API capability branch, so it works both inside/outside fullscreen and in
browsers without Fullscreen API support.

## TDD evidence

Baseline:

- `dotnet test GiselX.sln --no-restore`
  - PASS: 297 passed, 2 skipped, 0 failed, 299 total.

Finding 1 RED/GREEN:

- RED: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~PowerBiReportMenuViewComponentTests|FullyQualifiedName~PowerBiReportMenuServiceTests|FullyQualifiedName~PowerBiReportRepositoryContractTests"`
  - Expected compile failure: `IPowerBiReportMenuRepository` and
    `IPowerBiReportMenuService` did not exist (`CS0246`).
- First GREEN compilation exposed a test-only missing `UserClaimDto` using; corrected.
- Next GREEN attempt: 53 passed, 1 failed. The production path was correct, but the
  deliberately incomplete fake connection threw during Dapper command construction
  before reaching `OpenAsync`, leaving `OpenCount` at zero. The fake was completed at
  the command/parameter boundary so the regression genuinely exercised open failure.
- GREEN: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~PowerBiReportMenuViewComponentTests|FullyQualifiedName~PowerBiReportMenuServiceTests|FullyQualifiedName~PowerBiReportRepositoryContractTests|FullyQualifiedName~PowerBiReportServiceTests"`
  - PASS: 54 passed, 0 failed.

Finding 2 RED/GREEN:

- RED: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~PowerBiReportServiceTests|FullyQualifiedName~PowerBiReportsAdminControllerTests"`
  - Expected compile failure: `ServiceValidationError` and `ValidationErrors` did not
    exist (`CS0246`, `CS0117`, `CS1061`).
- First GREEN: 45 passed, 1 failed. This identified the duplicate pre-check still using
  the legacy unkeyed helper; it was routed through the same keyed duplicate result as
  the uniqueness-race catch.
- GREEN: the same command passed 46 passed, 0 failed.

Finding 3 RED/GREEN:

- RED: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~PowerBiReportRepositoryContractTests`
  - Expected failures: 2 failed, 19 passed. Real query order was
    `[101, 104, 102, 103]` instead of `[104, 102, 103, 101]`, and the SQL lacked the
    Company join/name ordering.
- GREEN: the same command passed 21 passed, 0 failed.

Finding 4 RED/GREEN:

- RED: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~PowerBiReportsControllerTests`
  - Expected failures: 2 failed, 9 passed. Razor lacked `power-bi-retry`; JavaScript
    lacked the retry/reset/reload path.
- GREEN: the same command passed 11 passed, 0 failed.

## Final automated verification

- Feature filter: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~PowerBi`
  - PASS: 107 passed, 0 skipped, 0 failed.
- Initial format verification found two whitespace issues in
  `GiselX.Repository/ServiceCollectionExtensions.cs` around existing mixed line endings.
- Formatting: `dotnet format GiselX.sln --no-restore --include <all changed production C# files>`
  - PASS; workspace-load warning only.
- Format verification: `dotnet format GiselX.sln --no-restore --verify-no-changes --include <all changed production C# files>`
  - PASS; workspace-load warning only.
- `git diff --check`
  - PASS; no whitespace errors. Git emitted only its line-ending normalization notices.
- Full tests: `dotnet test GiselX.sln --no-restore`
  - PASS: 304 passed, 2 skipped, 0 failed, 306 total.
  - The skipped tests are the pre-existing LocalDB/manual SQL checks; they are not
    claimed as passed.
- Full build: `dotnet build GiselX.sln --no-restore`
  - PASS: 0 errors, 6 warnings. Warnings are the existing NuGet vulnerability notices
    for `NuGet.Packaging`/`NuGet.Protocol` 6.12.1 and
    `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

## Explicitly unclaimed manual/environment checks

This wave did not run or claim the blocked disposable-SQL deployment/application
matrix, authenticated browser matrix, or physical fullscreen/connectivity checks. The
automated repository, service, controller, Razor, and JavaScript contracts above cover
the CODE findings; runtime-environment handoff remains separate as instructed.

The Publish-to-web warning remains applicable: GiselX menu authorization does not make
the configured Power BI URL private. Apply migration/manual deployment script
`20260821095349_AddPowerBiReports` before using the management page in a target
environment.
