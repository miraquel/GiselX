# Multi-Week Sellout Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Accept a mapped workbook containing one or more reporting weeks, warn the uploader before replacement, and atomically store every valid transaction under its calculated weekly batch.

**Architecture:** A shared workbook-analysis service will detect the company mapping, classify trailing summary rows, validate transaction rows, and calculate Monday week groups for both preflight and confirmed upload. A new upload-group aggregate will own one file attempt and link the existing one-week `SelloutUploadBatch` records; set-based repository promotion will replace every affected company/week in the request transaction. The MVC page will preflight through an anti-forgery-protected endpoint and require explicit confirmation for multi-week files.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC, EF Core SQL Server migrations, Dapper, xUnit, Razor, Bootstrap, JavaScript.

## Global Constraints

- Keep automatic mapping detection company-scoped and require exactly one active exact header-signature match.
- Keep the import all-or-nothing across every reporting week in the workbook.
- Calculate `ReportingWeekStart` exclusively from each validated `InvoiceDate`.
- Never trust week ranges or counts sent by the browser; repeat analysis on confirmed upload.
- Ignore only a trailing suffix of numeric summary rows with all mapped identity fields blank, and report every ignored source row.
- Preserve existing single-week batch rows and old `UploadResult?batchId=` links.
- Preserve unrelated dirty-worktree changes and stage only files belonging to the current task.

---

### Task 1: Multi-week workbook analysis

**Files:**
- Create: `GiselX.Service.Dto/SelloutWeekSummaryDto.cs`
- Create: `GiselX.Service.Dto/SelloutWorkbookAnalysisDto.cs`
- Modify: `GiselX.Service/SelloutParseResult.cs`
- Modify: `GiselX.Service/SelloutWorkbookParser.cs`
- Test: `GiselX.Tests/SelloutWorkbookParserTests.cs`

**Interfaces:**
- Produces: `SelloutWorkbookParser.Parse(byte[] workbook, DistributorTemplateMappingDto mapping)`.
- Produces: `SelloutParseResult.WeekSummaries` and `SelloutParseResult.IgnoredSummaryRowNumbers`.
- Produces: `SelloutWeekSummaryDto { DateTime ReportingWeekStart; int RowCount; }`.
- Consumes: existing `XlsxSelloutWorkbookInspector.ReadWorksheet`, mapping columns, canonical conversions, and validation catalog.

- [ ] **Step 1: Write failing parser tests for Monday grouping**

Add tests that parse invoice dates `2026-07-04`, `2026-07-06`, and `2026-07-31` and assert ordered groups `(2026-06-29, 1)`, `(2026-07-06, 1)`, `(2026-07-27, 1)`. Remove the selected-week argument from the wished-for helper API.

```csharp
Assert.Collection(result.WeekSummaries,
    week => Assert.Equal((new DateTime(2026, 6, 29), 1), (week.ReportingWeekStart, week.RowCount)),
    week => Assert.Equal((new DateTime(2026, 7, 6), 1), (week.ReportingWeekStart, week.RowCount)),
    week => Assert.Equal((new DateTime(2026, 7, 27), 1), (week.ReportingWeekStart, week.RowCount)));
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~SelloutWorkbookParserTests --no-restore
```

Expected: compilation fails because `WeekSummaries` and the week-free parser signature do not exist.

- [ ] **Step 3: Implement week calculation and remove selected-week rejection**

Add a helper with deterministic Monday arithmetic and build summaries only after all rows have been converted and validated:

```csharp
internal static DateTime GetMonday(DateTime date)
{
    var offset = ((int)date.DayOfWeek + 6) % 7;
    return date.Date.AddDays(-offset);
}
```

Delete only the validation branch that compares invoice dates with a selected week. Keep required, conversion, quantity, and length validation unchanged.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the Task 1 command and expect all parser tests to pass.

- [ ] **Step 5: Write failing tests for trailing-summary classification**

Cover these independent behaviors:

```csharp
[Fact] public void Parse_ignores_and_reports_trailing_numeric_summary_rows()
[Fact] public void Parse_keeps_summary_shaped_middle_row_as_invalid_transaction()
[Fact] public void Parse_keeps_identity_bearing_trailing_row_as_invalid_transaction()
```

The first case ends with a row containing only mapped `InvoiceQty` and `NetValue`; assert it is absent from `Rows` and present in `IgnoredSummaryRowNumbers`. The second places the same shape between valid rows; assert it remains staged and invalid. The third supplies `CustomerId` in the final row; assert normal required-field errors.

- [ ] **Step 6: Run the summary tests and verify RED**

Expected: the footer test reports missing required values and the new ignored-row property is missing.

- [ ] **Step 7: Implement suffix-only footer classification**

Before conversion, collect nonblank mapped source rows. Walk backward while identity raw values are blank and at least one mapped integer/decimal raw value is populated. Ignore that suffix only when a preceding transaction-shaped row exists. Retain source row numbers in ascending order.

- [ ] **Step 8: Run all parser tests and verify GREEN**

Run the Task 1 command and expect all tests to pass.

- [ ] **Step 9: Commit the parser increment**

```powershell
git add GiselX.Service.Dto/SelloutWeekSummaryDto.cs GiselX.Service.Dto/SelloutWorkbookAnalysisDto.cs GiselX.Service/SelloutParseResult.cs GiselX.Service/SelloutWorkbookParser.cs GiselX.Tests/SelloutWorkbookParserTests.cs
git commit -m "feat: analyze multi-week sellout workbooks"
```

### Task 2: Upload-group persistence model

**Files:**
- Create: `GiselX.Domain/SelloutUploadGroup.cs`
- Create: `GiselX.Domain/SelloutUploadGroupStatus.cs`
- Modify: `GiselX.Domain/Company.cs`
- Modify: `GiselX.Domain/SelloutUploadBatch.cs`
- Modify: `GiselX.Web/Context/GiselXDbContext.cs`
- Create: `GiselX.Web/Context/Migrations/<timestamp>_AddMultiWeekSelloutUploadGroup.cs`
- Create: `GiselX.Web/Context/Migrations/<timestamp>_AddMultiWeekSelloutUploadGroup.Designer.cs`
- Modify: `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`
- Test: `GiselX.Tests/SelloutPersistenceModelTests.cs`

**Interfaces:**
- Produces: `SelloutUploadGroup` with group audit, validation JSON, counts, and `ICollection<SelloutUploadBatch> Batches`.
- Produces: nullable `SelloutUploadBatch.SelloutUploadGroupId` for legacy compatibility.
- Consumes: existing `Company`, mapping, and batch foreign-key conventions.

- [ ] **Step 1: Write failing EF model tests**

Assert that the model contains `SelloutUploadGroup`, a restricted company FK, a restricted nullable mapping FK, a cascade group-to-batches relationship, JSON columns using `nvarchar(max)`, and an index on `(CompanyId, UploadedDate)`.

- [ ] **Step 2: Run the model tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~SelloutPersistenceModelTests --no-restore
```

Expected: the group entity cannot be found.

- [ ] **Step 3: Implement the domain model and EF configuration**

Use this status contract:

```csharp
public enum SelloutUploadGroupStatus { Processing = 0, Failed = 1, Completed = 2 }
```

Store `IgnoredSummaryRowsJson` and `ValidationErrorsJson` as non-null JSON arrays (`"[]"`). Keep batch filename/hash fields populated for compatibility even though the group is authoritative for new uploads.

- [ ] **Step 4: Generate and inspect the migration**

Run:

```powershell
dotnet ef migrations add AddMultiWeekSelloutUploadGroup --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext
```

Verify `Up` creates the group table and nullable batch FK without rewriting existing batch rows; verify `Down` removes only the new FK, column, index, and table.

- [ ] **Step 5: Run model tests and verify GREEN**

Run the Task 2 test command and expect all persistence model tests to pass.

- [ ] **Step 6: Commit the persistence model**

Stage only the Task 2 files and commit with `feat: add sellout upload groups`.

### Task 3: Shared preflight analysis service

**Files:**
- Create: `GiselX.Service.Dto/SelloutUploadPreflightDto.cs`
- Create: `GiselX.Service.Interface/ISelloutWorkbookAnalysisService.cs`
- Create: `GiselX.Service/SelloutWorkbookAnalysisService.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Test: `GiselX.Tests/SelloutWorkbookAnalysisServiceTests.cs`

**Interfaces:**
- Produces: `AnalyzeAsync(byte[] content, int companyId, CancellationToken)` returning detected mapping, parsed rows, week summaries, ignored row numbers, and errors.
- Consumes: `IDistributorTemplateMappingRepository.GetActiveByCompanyAsync`, `ISelloutWorkbookInspector.DetectAsync`, and `SelloutWorkbookParser.Parse`.

- [ ] **Step 1: Write failing service tests**

Using small hand-written fakes, cover exact mapping success, zero/multiple mapping failure propagation, ordered multi-week summaries, and no repository mutation. Assert `IsMultiWeek` is derived from `Weeks.Count > 1` rather than supplied by a caller.

- [ ] **Step 2: Run and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~SelloutWorkbookAnalysisServiceTests --no-restore
```

Expected: analysis service types are missing.

- [ ] **Step 3: Implement the shared analysis service**

Map domain mappings to the existing DTO shape in one private method and return a structured result even when row validation fails. Do not open a transaction or call upload persistence.

- [ ] **Step 4: Run and verify GREEN**

Run the Task 3 command and expect all analysis tests to pass.

- [ ] **Step 5: Commit the analysis service**

Stage only Task 3 files and commit with `feat: add sellout upload preflight analysis`.

### Task 4: Atomic grouped upload orchestration

**Files:**
- Modify: `GiselX.Repository.Interface/ISelloutUploadRepository.cs`
- Modify: `GiselX.Repository/SelloutUploadRepository.cs`
- Modify: `GiselX.Service.Interface/ISelloutUploadService.cs`
- Modify: `GiselX.Service/SelloutUploadService.cs`
- Modify: `GiselX.Service.Dto/SelloutUploadResultDto.cs`
- Create: `GiselX.Service.Dto/SelloutUploadGroupDto.cs`
- Modify: `GiselX.Service.Dto/SelloutUploadBatchDto.cs`
- Test: `GiselX.Tests/SelloutUploadServiceTests.cs`

**Interfaces:**
- Produces: `PreflightAsync(Stream file, string originalFileName, int companyId, CancellationToken)`.
- Produces: `UploadAsync(Stream file, string originalFileName, int companyId, bool multiWeekConfirmed, string uploadedBy, CancellationToken)`.
- Produces: `GetUploadResultAsync(long? groupId, long? batchId, int companyId, CancellationToken)`.
- Produces repository operations to insert/mark groups, insert weekly batches, insert staging rows, promote all group batches, and retrieve group results.

- [ ] **Step 1: Write failing preflight and confirmation tests**

Cover bounded file reading, `.xlsx` validation, preflight no-write behavior, rejection of a multi-week upload when `multiWeekConfirmed` is false, and successful continuation when true. Assert the server analysis dependency is invoked independently for preflight and upload.

- [ ] **Step 2: Run and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~SelloutUploadServiceTests --no-restore
```

Expected: new service methods/signatures are absent.

- [ ] **Step 3: Implement minimal preflight and confirmation guard**

Keep filename/size validation in the upload service. Return this explicit guard message when needed:

```text
This workbook contains multiple reporting weeks. Review the affected weeks and confirm the upload before continuing.
```

- [ ] **Step 4: Run preflight tests and verify GREEN**

Run the focused command and expect the initial tests to pass.

- [ ] **Step 5: Write failing orchestration tests**

Assert one created batch per week, each row assigned to the correct created batch ID, group and batch counts, a single final commit, and no promotion when any parsed row is invalid. Add a failure-path test proving an exception during the second week results in rollback/no commit.

- [ ] **Step 6: Run orchestration tests and verify RED**

Expected: the current service creates only one selected-week batch and cannot group rows.

- [ ] **Step 7: Implement grouped orchestration and set-based promotion**

Insert the group, insert ordered weekly child batches, assign staging batch IDs by `GetMonday(row.InvoiceDate.Value)`, and call one repository `PromoteGroupAsync(groupId, cancellationToken)`. In SQL, lock the group and all affected weeks, reject invalid staging, mark prior completed batches replaced per week, delete only matching company/week rows, insert from valid staging with the child batch week, complete children, and complete the group.

- [ ] **Step 8: Run service tests and verify GREEN**

Run Task 4 tests, then all mapping/upload tests. Expect no failures.

- [ ] **Step 9: Commit the orchestration increment**

Stage Task 4 files only and commit with `feat: import sellout workbooks by reporting week`.

### Task 5: Preflight confirmation and grouped result UI

**Files:**
- Modify: `GiselX.Web/Controllers/SalesTransactionsController.cs`
- Modify: `GiselX.Web/Views/SalesTransactions/Index.cshtml`
- Modify: `GiselX.Web/Views/SalesTransactions/UploadResult.cshtml`
- Test: `GiselX.Tests/SalesTransactionUploadControllerTests.cs`

**Interfaces:**
- Produces: POST `/SalesTransactions/PreflightUpload` returning structured JSON.
- Produces: POST `/SalesTransactions/Upload` accepting `file` and `multiWeekConfirmed`.
- Preserves: GET `/SalesTransactions/UploadResult?batchId=...`; adds `groupId` routing.

- [ ] **Step 1: Write failing controller tests**

Cover permission-protected preflight, current-company forwarding, structured JSON, confirmation forwarding, grouped redirect, old batch result routing, null-user challenge, and cancellation token forwarding. Use hand-written service fakes consistent with existing controller tests.

- [ ] **Step 2: Run and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~SalesTransactionUploadControllerTests --no-restore
```

Expected: preflight action and new service signatures are missing.

- [ ] **Step 3: Implement controller actions and grouped result model**

Both POST actions require `[Authorize(PermissionConstants.SalesTransactions.Upload)]` and `[ValidateAntiForgeryToken]`. Resolve company exclusively from the authenticated user.

- [ ] **Step 4: Run controller tests and verify GREEN**

Run the Task 5 command and expect all tests to pass.

- [ ] **Step 5: Implement the confirmation interface**

Remove `reportingWeekStart`. Intercept form submission, post `FormData` to preflight with the anti-forgery token, and render a Bootstrap modal whose week rows are created with DOM APIs and `textContent`. The modal shows mapping, totals, ignored rows, and this multi-week message:

```text
This workbook contains {count} reporting weeks. Confirming will replace the current sellout data for every week listed below.
```

On confirmation, set hidden `multiWeekConfirmed=true` and submit the unchanged file form normally. Disable buttons during both requests and restore them on errors.

- [ ] **Step 6: Update grouped result rendering**

Render overall group status plus week-start, week-end, row count, and batch status. Continue rendering row/field errors with Razor encoding and show ignored summary rows as informational, not failed data.

- [ ] **Step 7: Run Razor compilation and focused tests**

Run:

```powershell
dotnet build GiselX.Web/GiselX.Web.csproj --no-restore
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~SalesTransactionUploadControllerTests|FullyQualifiedName~SelloutUploadServiceTests" --no-restore
```

Expected: build succeeds and focused tests pass.

- [ ] **Step 8: Commit the UI increment**

Stage Task 5 files only and commit with `feat: confirm multi-week sellout uploads`.

### Task 6: Full verification with the supplied GMK workbook

**Files:**
- Modify only if a verification-discovered defect requires a new failing regression test and minimal fix.

**Interfaces:**
- Consumes the completed feature through tests, migration inspection, and the running MVC application.

- [ ] **Step 1: Run the focused mapping and upload suite**

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~Sellout|FullyQualifiedName~DistributorTemplateMapping|FullyQualifiedName~SalesTransactionUpload" --no-restore
```

Expected: all focused tests pass.

- [ ] **Step 2: Run the full solution tests with isolated artifacts if the local server locks output**

```powershell
dotnet test GiselX.sln --no-restore -p:BaseOutputPath="$env:TEMP\giselx-multiweek\bin\" -p:BaseIntermediateOutputPath="$env:TEMP\giselx-multiweek\obj\"
```

Expected: all tests pass. Existing package-advisory warnings may be reported separately; no compile or test failures are accepted.

- [ ] **Step 3: Inspect the migration script**

```powershell
dotnet ef migrations script 20260807102708_AddDistributorSelloutMapping AddMultiWeekSelloutUploadGroup --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext --no-build
```

Verify that no unrelated table or column is dropped or rewritten.

- [ ] **Step 4: Browser-check the supplied workbook**

Use `C:\Users\ahmad\Downloads\GMK 1-31 JULI 2026.xlsx`. Verify preflight reports mapping `Mapping Sellout`, 305 transaction rows, ignored row 310, and five week counts: 49, 78, 75, 41, and 62. Cancel once and verify no writes; then confirm and verify 305 final `SalesTransaction` rows across week starts `2026-06-29`, `2026-07-06`, `2026-07-13`, `2026-07-20`, and `2026-07-27`.

- [ ] **Step 5: Review the final diff and commit any verification fix**

Run `git diff --check` and `git status --short`. If a defect was found, add a failing regression test first, implement the minimal fix, re-run verification, and commit only those files.
