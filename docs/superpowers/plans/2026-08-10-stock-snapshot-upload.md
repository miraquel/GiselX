# Stock Snapshot Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add company-mapped, preflighted Stock snapshot uploads that stage every row, publish valid non-negative balances, preserve immutable history, show the latest completed snapshot, and download any completed historical batch.

**Architecture:** A Stock-specific mapping and analysis pipeline mirrors the safe Sales Transaction boundaries without introducing a redundant upload group. One `StockUploadBatch` owns each selected StockDate workbook; every parsed row is retained in `StockStaging`, while valid rows are appended to `Stock` and linked to the batch. Normal queries select the latest successful batch, and historical downloads select an explicitly authorized completed batch.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC, EF Core SQL Server migrations, Dapper/SqlBulkCopy, ClosedXML, Bootstrap, JavaScript, xUnit.

## Global Constraints

- Keep Stock mappings isolated from Sales Transaction mappings and company-scoped.
- Require an explicit user-selected `StockDate`; never derive it from `CreatedDate`, filename, or workbook title.
- Preflight performs no database writes and confirmed upload repeats all analysis server-side.
- Stage every confirmed non-summary data row, including conversion failures and negative `SaldoAkhir` rows.
- Publish only rows that pass required, conversion, length, and `SaldoAkhir >= 0` rules.
- Complete with warnings when at least one row is valid and at least one is invalid; fail when zero rows are valid.
- Never delete or replace a prior Stock snapshot.
- Define latest as greatest successful completion timestamp, then greatest batch ID.
- Scope mapping, upload results, latest queries, dropdowns, and downloads to the authenticated company.
- Preserve existing Stock rows with nullable new foreign key and StockDate columns.
- Do not store original workbook bytes or add an administrator history page.
- Preserve unrelated workspace changes. Git is currently unavailable on this machine; run commit steps only when Git becomes available.

---

### Task 1: Stock canonical fields, strict parser, and row classification

**Files:**
- Create: `GiselX.Domain/StockCanonicalField.cs`
- Create: `GiselX.Service/StockCanonicalFieldCatalog.cs`
- Create: `GiselX.Service.Dto/StockTemplateColumnDto.cs`
- Create: `GiselX.Service.Dto/StockTemplateMappingDto.cs`
- Create: `GiselX.Service.Dto/StockStagingErrorDto.cs`
- Create: `GiselX.Service.Dto/StockStagingRowDto.cs`
- Create: `GiselX.Service.Dto/StockParseResult.cs`
- Create: `GiselX.Service/StockCellValueConverter.cs`
- Create: `GiselX.Service/StockWorkbookParser.cs`
- Test: `GiselX.Tests/StockCanonicalFieldCatalogTests.cs`
- Test: `GiselX.Tests/StockWorkbookParserTests.cs`

**Interfaces:**
- Produces: `StockCanonicalField` with the 11 fields from the design.
- Produces: `StockCanonicalFieldCatalog.RequiredFields`, `GetValueType`, and `GetMaxLength`.
- Produces: `StockWorkbookParser.Parse(byte[] workbook, StockTemplateMappingDto mapping)` returning `StockParseResult`.
- Consumes: `XlsxSelloutWorkbookInspector.ReadWorksheet(byte[], string)` for strict cell extraction; keep that helper internal and format-neutral or extract it to `XlsxWorkbookReader` if its name blocks clean reuse.

- [ ] **Step 1: Write failing canonical catalog tests**

Add tests asserting required fields and value types:

```csharp
[Fact]
public void Required_fields_are_product_identity_and_closing_balance()
{
    Assert.Equal(
        [StockCanonicalField.ProductId, StockCanonicalField.ProductName, StockCanonicalField.SaldoAkhir],
        StockCanonicalFieldCatalog.RequiredFields.Order());
}

[Theory]
[InlineData(StockCanonicalField.ProductPcsInCtn, typeof(int))]
[InlineData(StockCanonicalField.ProductNetto, typeof(decimal))]
[InlineData(StockCanonicalField.SaldoAkhir, typeof(decimal))]
[InlineData(StockCanonicalField.ExpiredDate, typeof(DateTime))]
public void Catalog_exposes_strict_types(StockCanonicalField field, Type expected) =>
    Assert.Equal(expected, StockCanonicalFieldCatalog.GetValueType(field));
```

- [ ] **Step 2: Run the catalog tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockCanonicalFieldCatalogTests
```

Expected: compilation fails because Stock canonical types do not exist.

- [ ] **Step 3: Implement the enum and catalog**

Use this exact enum:

```csharp
public enum StockCanonicalField
{
    ProductId, ProductName, ProductPackaging, ProductPcsInCtn, ProductNetto,
    ProductUnit, SaldoAwal, SaldoMasukPO, SaldoAkhir, BatchNumber, ExpiredDate
}
```

Set maximum lengths to match the existing final model/schema: ProductId 100, ProductName 200, ProductPackaging 100, ProductUnit 50, and BatchNumber 100. Return `null` for numeric/date lengths.

- [ ] **Step 4: Run catalog tests and verify GREEN**

Run the Step 2 command and expect all tests to pass.

- [ ] **Step 5: Write failing parser tests**

Cover strict conversion, staging-invalid rows, negative balance, summary suffix, and zero balance:

```csharp
[Fact] public void Parse_accepts_zero_saldo_akhir_for_publication()
[Fact] public void Parse_marks_negative_saldo_akhir_invalid_but_keeps_the_row()
[Fact] public void Parse_marks_invalid_optional_date_invalid_without_fallback()
[Fact] public void Parse_ignores_and_reports_trailing_numeric_summary()
[Fact] public void Parse_keeps_summary_shaped_middle_row_as_invalid_data()
[Fact] public void Parse_keeps_identity_bearing_trailing_row_as_data()
```

The negative test must assert both retention and validation:

```csharp
var row = Assert.Single(result.Rows);
Assert.Equal(-1m, row.SaldoAkhir);
Assert.False(row.IsValid);
Assert.Contains(result.Errors, error =>
    error.SourceRowNumber == 3 &&
    error.Field == StockCanonicalField.SaldoAkhir &&
    error.Reason == "SaldoAkhir must not be negative.");
```

- [ ] **Step 6: Run parser tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockWorkbookParserTests
```

Expected: missing parser/result types.

- [ ] **Step 7: Implement strict parsing**

`StockParseResult` must expose:

```csharp
public sealed record StockParseResult(
    IReadOnlyList<StockStagingRowDto> Rows,
    IReadOnlyList<StockStagingErrorDto> Errors,
    string? BatchError,
    IReadOnlyList<int> IgnoredSummaryRowNumbers)
{
    public int ValidRows => Rows.Count(row => row.IsValid);
    public int InvalidRows => Rows.Count - ValidRows;
}
```

The parser must create one `StockStagingRowDto` for every nonblank, non-summary source row. The DTO contains `SourceRowNumber`, nullable values for all 11 canonical fields, `IsValid`, and `ValidationErrorsJson`. Conversion errors leave the typed property null and append a `StockStagingErrorDto` containing source row, canonical field, raw rejected value, and reason. Required checks run after conversion. Mark `SaldoAkhir < 0` invalid; accept zero. Ignore only the trailing summary suffix defined in the spec.

- [ ] **Step 8: Run parser tests and verify GREEN**

Run the Step 6 command and expect all parser tests to pass.

- [ ] **Step 9: Commit the parser increment**

```powershell
git add GiselX.Domain\StockCanonicalField.cs GiselX.Service\StockCanonicalFieldCatalog.cs GiselX.Service.Dto\StockTemplateColumnDto.cs GiselX.Service.Dto\StockTemplateMappingDto.cs GiselX.Service.Dto\StockStagingErrorDto.cs GiselX.Service.Dto\StockStagingRowDto.cs GiselX.Service.Dto\StockParseResult.cs GiselX.Service\StockCellValueConverter.cs GiselX.Service\StockWorkbookParser.cs GiselX.Tests\StockCanonicalFieldCatalogTests.cs GiselX.Tests\StockWorkbookParserTests.cs
git commit -m "feat: parse mapped stock snapshots"
```

---

### Task 2: Company-specific Stock mapping persistence and administration

**Files:**
- Create: `GiselX.Domain/StockTemplateMapping.cs`
- Create: `GiselX.Domain/StockTemplateColumn.cs`
- Modify: `GiselX.Domain/Company.cs`
- Create: `GiselX.Repository.Interface/IStockTemplateMappingRepository.cs`
- Create: `GiselX.Repository/StockTemplateMappingRepository.cs`
- Create: `GiselX.Service.Dto/SaveStockTemplateMappingDto.cs`
- Create: `GiselX.Service.Interface/IStockTemplateMappingService.cs`
- Create: `GiselX.Service/StockTemplateMappingService.cs`
- Create: `GiselX.Service.Interface/IStockWorkbookInspector.cs`
- Create: `GiselX.Service/XlsxStockWorkbookInspector.cs`
- Create: `GiselX.Web/Models/StockTemplateMappingEditViewModel.cs`
- Create: `GiselX.Web/Models/StockTemplateMappingListViewModel.cs`
- Create: `GiselX.Web/Controllers/StockTemplateMappingsController.cs`
- Create: `GiselX.Web/Areas/Admin/Views/StockTemplateMappings/Index.cshtml`
- Create: `GiselX.Web/Areas/Admin/Views/StockTemplateMappings/Edit.cshtml`
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml`
- Modify: `GiselX.Repository/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Test: `GiselX.Tests/StockTemplateMappingServiceTests.cs`
- Test: `GiselX.Tests/StockTemplateMappingControllerTests.cs`

**Interfaces:**
- Produces repository methods `GetByCompanyAsync`, `GetActiveByCompanyAsync`, `GetByIdAsync`, `InsertAsync`, `UpdateAsync`, `SetActiveAsync`, and `DeleteAsync`, all company-scoped.
- Produces service methods matching the existing distributor mapping CRUD shape but using Stock mapping DTOs.
- Produces `IStockWorkbookInspector.InspectAsync` and `DetectAsync` using exact ordered header signatures.
- Consumes Task 1 canonical fields and mapping DTOs.

- [ ] **Step 1: Write failing mapping service tests**

Cover required assignments, duplicate source/canonical assignments, cross-company update rejection, and one-active-signature behavior:

```csharp
[Fact] public async Task Save_rejects_missing_required_stock_fields()
[Fact] public async Task Save_rejects_duplicate_source_columns()
[Fact] public async Task Save_rejects_duplicate_canonical_fields()
[Fact] public async Task Save_cannot_update_another_company_mapping()
[Fact] public async Task SetActive_is_scoped_to_company()
```

Assert the missing-required error names `ProductId`, `ProductName`, and `SaldoAkhir`.

- [ ] **Step 2: Run mapping service tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockTemplateMappingServiceTests
```

Expected: missing Stock mapping domain/service types.

- [ ] **Step 3: Implement domain, repository, service, and inspector**

Use restricted Company deletion and cascade mapping-to-columns deletion. The inspector must normalize headers exactly as the Sellout inspector does, but accept and return Stock mapping DTOs. Detection results must distinguish zero matches, one match, and multiple matches with stable error messages.

- [ ] **Step 4: Run mapping service tests and verify GREEN**

Run Step 2 and expect all tests to pass.

- [ ] **Step 5: Write failing controller contract tests**

Assert the controller is in the Admin area, requires `PermissionConstants.Companies.Edit`, preserves company ID on list/create/edit, inspects an uploaded sample, and saves only explicitly assigned columns.

- [ ] **Step 6: Implement admin controller and views**

Use the route/controller name `StockTemplateMappings`. Add these Company Edit actions:

```html
<a asp-controller="StockTemplateMappings" asp-action="Index" asp-route-companyId="@Model.Id"
   class="btn btn-outline-primary">Manage Stock Mappings</a>
<a asp-controller="StockTemplateMappings" asp-action="Create" asp-route-companyId="@Model.Id"
   class="btn btn-outline-secondary">Add Stock Mapping</a>
```

The Edit view must preview sample headers, provide a canonical-field dropdown per source column, and post anti-forgery-protected save/activate/delete operations.

- [ ] **Step 7: Run mapping/controller tests and build Razor**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~StockTemplateMappingServiceTests|FullyQualifiedName~StockTemplateMappingControllerTests"
dotnet build GiselX.Web\GiselX.Web.csproj --no-restore -p:BaseOutputPath=.tmp\stock-mapping\bin\
```

Expected: tests pass and Razor compiles.

- [ ] **Step 8: Commit mapping administration**

Stage only Task 2 files and commit with:

```powershell
git commit -m "feat: configure distributor stock mappings"
```

---

### Task 3: Stock batch, staging, and immutable final-row persistence model

**Files:**
- Create: `GiselX.Domain/StockUploadBatchStatus.cs`
- Create: `GiselX.Domain/StockUploadBatch.cs`
- Create: `GiselX.Domain/StockStaging.cs`
- Modify: `GiselX.Domain/Stock.cs`
- Modify: `GiselX.Domain/Company.cs`
- Modify: `GiselX.Web/Context/GiselXDbContext.cs`
- Create: `GiselX.Web/Context/Migrations/20260810160000_AddStockSnapshotUploads.cs`
- Create: `GiselX.Web/Context/Migrations/20260810160000_AddStockSnapshotUploads.Designer.cs`
- Modify: `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`
- Test: `GiselX.Tests/StockPersistenceModelTests.cs`

**Interfaces:**
- Produces: `StockUploadBatchStatus { Processing = 0, Failed = 1, Completed = 2, CompletedWithWarnings = 3 }`.
- Produces: nullable `Stock.StockUploadBatchId` and `Stock.StockDate`.
- Produces: one-to-many Batch→Staging and Batch→Stock relationships.
- Consumes: Task 2 `StockTemplateMapping`.

- [ ] **Step 1: Write failing EF model tests**

Assert:

```csharp
Assert.Equal(DeleteBehavior.Restrict, BatchCompanyForeignKey.DeleteBehavior);
Assert.Equal(DeleteBehavior.Restrict, BatchMappingForeignKey.DeleteBehavior);
Assert.Equal(DeleteBehavior.Cascade, StagingBatchForeignKey.DeleteBehavior);
Assert.Equal(DeleteBehavior.Restrict, FinalStockBatchForeignKey.DeleteBehavior);
Assert.True(StockBatchIdProperty.IsNullable);
Assert.True(StockDateProperty.IsNullable);
```

Also assert indexes on `(CompanyId, CompletedDate, Id)`, `(CompanyId, StockDate)`, and `StockUploadBatchId`.

- [ ] **Step 2: Run model tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockPersistenceModelTests
```

Expected: missing batch and staging entities.

- [ ] **Step 3: Implement EF model configuration**

Configure filenames at 260, hashes as fixed `nchar(64)`, uploader at 256, validation JSON as non-null `nvarchar(max)` defaulting to `[]`, and all nullable staging value fields using final-table decimal precision and string lengths.

- [ ] **Step 4: Generate the migration**

```powershell
dotnet ef migrations add AddStockSnapshotUploads --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext
```

If `dotnet-ef` is unavailable, create the migration explicitly with the exact model configuration and update the snapshot, then record that limitation in verification.

- [ ] **Step 5: Inspect migration safety**

Verify `Up` creates four new tables (`StockTemplateMapping`, `StockTemplateColumn`, `StockUploadBatch`, `StockStaging`) and adds only nullable `StockUploadBatchId`/`StockDate` to `Stock`. Verify it never deletes or rewrites existing Stock rows. Verify `Down` removes only new objects.

- [ ] **Step 6: Run model tests and verify GREEN**

Run Step 2 and expect all tests to pass.

- [ ] **Step 7: Commit the persistence model**

Stage Task 3 files plus Task 2 mapping model configuration/migration content and commit:

```powershell
git commit -m "feat: add immutable stock upload batches"
```

---

### Task 4: Shared Stock workbook analysis and preflight

**Files:**
- Create: `GiselX.Service.Dto/StockWorkbookAnalysisDto.cs`
- Create: `GiselX.Service.Dto/StockUploadPreflightDto.cs`
- Create: `GiselX.Service.Interface/IStockWorkbookAnalysisService.cs`
- Create: `GiselX.Service/StockWorkbookAnalysisService.cs`
- Create: `GiselX.Service/StockUploadOptions.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Web/Program.cs`
- Modify: `GiselX.Web/appsettings.json`
- Test: `GiselX.Tests/StockWorkbookAnalysisServiceTests.cs`

**Interfaces:**
- Produces: `AnalyzeAsync(byte[] content, int companyId, CancellationToken)` returning mapping, every parsed row, errors, valid/invalid counts, and ignored rows.
- Produces: `StockUploadPreflightDto` with mapping name, StockDate supplied by the endpoint, total/valid/invalid counts, ignored rows, row errors, batch errors, and `CanPublish => ValidRows > 0 && Errors.Count == 0`.
- Consumes: Task 1 parser, Task 2 inspector and active mapping repository.

- [ ] **Step 1: Write failing analysis tests**

Cover one exact mapping, no match, multiple matches, partial-valid workbook, and no repository mutations:

```csharp
[Fact] public async Task Analyze_returns_all_rows_and_partial_valid_counts()
[Fact] public async Task Analyze_reports_no_exact_mapping()
[Fact] public async Task Analyze_reports_ambiguous_exact_mappings()
[Fact] public async Task Analyze_does_not_write_mapping_or_upload_state()
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockWorkbookAnalysisServiceTests
```

- [ ] **Step 3: Implement analysis and options**

Use `StockUploadOptions.SectionName = "StockUpload"` and configure `MaxFileSizeBytes` to the same current limit as Sellout. The service must return structured analysis for row validation failures; mapping/file-level errors go in `Errors` and make publication impossible.

- [ ] **Step 4: Run tests and verify GREEN**

Run Step 2 and expect all tests to pass.

- [ ] **Step 5: Commit analysis**

```powershell
git add GiselX.Service.Dto\StockWorkbookAnalysisDto.cs GiselX.Service.Dto\StockUploadPreflightDto.cs GiselX.Service.Interface\IStockWorkbookAnalysisService.cs GiselX.Service\StockWorkbookAnalysisService.cs GiselX.Service\StockUploadOptions.cs GiselX.Service\ServiceCollectionExtensions.cs GiselX.Web\Program.cs GiselX.Web\appsettings.json GiselX.Tests\StockWorkbookAnalysisServiceTests.cs
git commit -m "feat: analyze stock uploads before confirmation"
```

---

### Task 5: Confirmed upload, stage-all persistence, and partial publication

**Files:**
- Create: `GiselX.Service.Dto/StockUploadResultDto.cs`
- Create: `GiselX.Service.Dto/StockUploadBatchDto.cs`
- Create: `GiselX.Repository.Interface/IStockUploadRepository.cs`
- Create: `GiselX.Repository/StockUploadRepository.cs`
- Create: `GiselX.Service.Interface/IStockUploadService.cs`
- Create: `GiselX.Service/StockUploadService.cs`
- Modify: `GiselX.Repository/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Test: `GiselX.Tests/StockUploadServiceTests.cs`

**Interfaces:**
- Produces: `PreflightAsync(Stream file, string originalFileName, DateTime stockDate, int companyId, CancellationToken)`.
- Produces: `UploadAsync(Stream file, string originalFileName, DateTime stockDate, int companyId, bool confirmed, string uploadedBy, CancellationToken)`.
- Produces: `GetResultAsync(long batchId, int companyId, CancellationToken)`.
- Produces repository methods `InsertBatchAsync`, `InsertStagingAsync`, `MarkFailedAsync`, `PublishValidRowsAsync`, and `GetBatchAsync`.
- Consumes: Task 4 analysis and options, Task 3 entities, request `IDbTransaction`.

- [ ] **Step 1: Write failing upload service tests**

Required tests:

```csharp
[Fact] public async Task Preflight_performs_no_writes()
[Fact] public async Task Upload_requires_explicit_confirmation()
[Fact] public async Task Upload_stages_every_row_and_publishes_only_valid_rows()
[Fact] public async Task Upload_completes_with_warnings_when_any_row_is_invalid()
[Fact] public async Task Upload_fails_and_publishes_nothing_when_zero_rows_are_valid()
[Fact] public async Task Upload_appends_without_deleting_prior_batches_or_stock()
[Fact] public async Task Publication_exception_does_not_commit_any_new_state()
```

For mixed rows assert:

```csharp
Assert.Equal(3, repository.StagingRows.Count);
Assert.Equal(2, repository.PublishedSourceRows.Count);
Assert.Equal(StockUploadBatchStatus.CompletedWithWarnings, response.Data!.Status);
Assert.Equal((3, 2, 1), (response.Data.TotalRows, response.Data.ValidRows, response.Data.InvalidRows));
Assert.Equal(1, transaction.CommitCount);
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockUploadServiceTests
```

- [ ] **Step 3: Implement bounded preflight and confirmation guard**

Reject non-`.xlsx`, empty, oversized, unconfirmed, or `stockDate == default` requests before writes. Preflight must not insert a batch. Confirmed upload repeats analysis independently.

- [ ] **Step 4: Implement repository inserts and set-based publication**

`PublishValidRowsAsync` must execute one parameterized SQL operation that:

```sql
INSERT INTO Stock (
 ProductId, ProductName, ProductPackaging, ProductPcsInCtn, ProductNetto,
 ProductUnit, SaldoAwal, SaldoMasukPO, SaldoAkhir, BatchNumber, ExpiredDate,
 CompanyId, CreatedDate, StockUploadBatchId, StockDate)
SELECT
 COALESCE(s.ProductId,''), COALESCE(s.ProductName,''), COALESCE(s.ProductPackaging,''),
 COALESCE(s.ProductPcsInCtn,0), COALESCE(s.ProductNetto,0), COALESCE(s.ProductUnit,''),
 COALESCE(s.SaldoAwal,0), COALESCE(s.SaldoMasukPO,0), s.SaldoAkhir,
 COALESCE(s.BatchNumber,''), COALESCE(s.ExpiredDate,CONVERT(date,'17530101')),
 b.CompanyId, SYSUTCDATETIME(), b.Id, b.StockDate
FROM StockStaging s
JOIN StockUploadBatch b ON b.Id=s.StockUploadBatchId
WHERE b.Id=@BatchId AND b.CompanyId=@CompanyId AND b.Status=0 AND s.IsValid=1;
```

Then update the same batch to `Completed` or `CompletedWithWarnings`. Do not issue any `DELETE` or `UPDATE` against previous Stock rows/batches.

- [ ] **Step 5: Implement service status and transaction behavior**

Insert the batch and all staging rows. If valid count is zero, mark failed and commit diagnostic state once. Otherwise publish valid rows, complete status, and commit once. Allow exceptions before commit to propagate so request transaction disposal rolls back all new state.

- [ ] **Step 6: Run service tests and verify GREEN**

Run Step 2 and expect all tests to pass.

- [ ] **Step 7: Commit upload orchestration**

```powershell
git commit -m "feat: publish immutable stock snapshots"
```

---

### Task 6: Latest snapshot queries and historical batch downloads

**Files:**
- Create: `GiselX.Service.Dto/StockUploadHistoryOptionDto.cs`
- Modify: `GiselX.Repository.Interface/IStockRepository.cs`
- Modify: `GiselX.Repository/StockRepository.cs`
- Modify: `GiselX.Service.Interface/IStockService.cs`
- Modify: `GiselX.Service/StockService.cs`
- Modify: `GiselX.Mapper/MapperlyMapper.cs` only if the DTO gains `StockDate`/batch metadata that requires mapping
- Test: `GiselX.Tests/StockSnapshotQueryServiceTests.cs`

**Interfaces:**
- Produces: `SelectLatestAsync(PagedListRequestDto request, int companyId, CancellationToken)` through the existing authenticated service boundary.
- Produces: `ListCompletedUploadsAsync(CancellationToken)` returning current-company options ordered newest first.
- Produces: `DownloadBatchAsync(long batchId, int companyId, CancellationToken)` returning valid final rows only.
- Consumes: completed statuses from Task 3 and final rows from Task 5.

- [ ] **Step 1: Write failing latest-query tests**

Cover deterministic latest selection, warning batches, later failed batch, and company isolation:

```csharp
[Fact] public async Task Select_returns_only_latest_successful_company_batch()
[Fact] public async Task Completed_with_warnings_is_eligible_as_latest()
[Fact] public async Task Later_failed_batch_does_not_hide_previous_success()
[Fact] public async Task Latest_selection_breaks_timestamp_ties_by_batch_id()
```

- [ ] **Step 2: Write failing history/download tests**

```csharp
[Fact] public async Task History_lists_only_current_company_completed_batches_newest_first()
[Fact] public async Task History_label_contains_upload_time_stock_date_and_filename()
[Fact] public async Task Download_returns_only_final_rows_linked_to_selected_batch()
[Fact] public async Task Download_rejects_another_company_batch()
[Fact] public async Task Download_rejects_failed_or_processing_batch()
```

- [ ] **Step 3: Run query tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockSnapshotQueryServiceTests
```

- [ ] **Step 4: Implement latest query**

Use a CTE/subquery selecting exactly one eligible batch:

```sql
SELECT TOP (1) Id
FROM StockUploadBatch
WHERE CompanyId=@CompanyId AND Status IN (2,3)
ORDER BY CompletedDate DESC, Id DESC
```

Apply all paging/search/sort predicates only to `Stock` rows whose `StockUploadBatchId` equals that ID. Return an empty page when no eligible batch exists.

- [ ] **Step 5: Implement historical list and download queries**

List options from batches with statuses 2/3, current company, `ORDER BY CompletedDate DESC, Id DESC`. Download must join `Stock` to its batch and filter both `batch.Id=@BatchId` and `batch.CompanyId=@CompanyId`; never query by batch ID alone.

- [ ] **Step 6: Run tests and verify GREEN**

Run Step 3 and expect all query tests to pass.

- [ ] **Step 7: Commit query behavior**

```powershell
git commit -m "feat: query latest and historical stock snapshots"
```

---

### Task 7: Stock preflight modal, result page, latest table, and download dropdown

**Files:**
- Modify: `GiselX.Web/Controllers/StocksController.cs`
- Modify: `GiselX.Web/Controllers/Api/StocksController.cs`
- Modify: `GiselX.Web/Views/Stocks/Index.cshtml`
- Create: `GiselX.Web/Views/Stocks/UploadResult.cshtml`
- Create: `GiselX.Web/Models/StockIndexViewModel.cs`
- Test: `GiselX.Tests/StockUploadControllerTests.cs`

**Interfaces:**
- Produces POST `/Stocks/PreflightUpload` accepting `file` and `stockDate`.
- Produces POST `/Stocks/Upload` accepting `file`, `stockDate`, and `confirmed`.
- Produces GET `/Stocks/UploadResult?batchId=...`.
- Produces GET `/Stocks/DownloadBatch?batchId=...`.
- Preserves API `/api/Stocks/Select` but changes it to latest-snapshot semantics.
- Consumes Tasks 5 and 6 service methods.

- [ ] **Step 1: Write failing controller contract tests**

Assert authorization, anti-forgery, company scoping, date/confirmation forwarding, result redirect, and download checks:

```csharp
[Fact] public void Preflight_and_upload_are_antiforgery_protected_posts()
[Fact] public async Task Preflight_forwards_current_company_and_stock_date()
[Fact] public async Task Upload_forwards_confirmation_and_authenticated_username()
[Fact] public async Task Upload_redirects_to_batch_result()
[Fact] public async Task Result_cannot_read_another_company_batch()
[Fact] public async Task Download_cannot_read_another_company_batch()
```

- [ ] **Step 2: Run controller tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockUploadControllerTests
```

- [ ] **Step 3: Replace controller-side ClosedXML parsing**

Delete the current eleven-column parsing block from `StocksController.Upload`. Add `IStockUploadService` to the controller constructor, bind nullable `IFormFile`, validate nonempty input, resolve the current user, and delegate preflight/upload to `IStockUploadService`. Change `Index` to load a `StockIndexViewModel` whose `CompletedUploads` property comes from `IStockService.ListCompletedUploadsAsync`. Both POST endpoints require `[ValidateAntiForgeryToken]` and `PermissionConstants.Stocks.Upload`.

- [ ] **Step 4: Update the Stock index markup**

Remove Year/Month selectors. Add:

```html
<input type="date" id="stockDate" name="stockDate" class="form-control" required>
<input type="hidden" id="stockUploadConfirmed" name="confirmed" value="false">
<input type="file" id="stockFileInput" name="file" accept="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" required>
```

Add a Bootstrap confirmation modal showing mapping, StockDate, total/valid/excluded counts, ignored summary rows, and validation warnings. Build error/weekless summary rows with `document.createElement` and `textContent`. Use the correct anti-forgery selector `input[name="__RequestVerificationToken"]`.

- [ ] **Step 5: Add the history dropdown and download button**

Populate the select from `ListCompletedUploadsAsync`. Option text must be formatted server-side or with safe text as:

```text
10 Aug 2026 14:35 — Stock as of 31 Jul 2026 — CSU_Stock.xlsx
```

The button navigates only when a batch ID is selected. The controller resolves the current company and calls `DownloadBatchAsync`; filename format is `Stock_<Company>_<StockDate:yyyy-MM-dd>_<BatchId>.xlsx`.

- [ ] **Step 6: Implement result view**

Show a success alert for `Completed`, a warning alert for `CompletedWithWarnings`, and a danger alert for `Failed`. Always show total/published/excluded counts and render each error with source row, field, rejected value, and reason. State explicitly that excluded rows remain in staging.

- [ ] **Step 7: Update API and DataTable to latest-only loading**

Remove year/month parameters from the DataTable request and call the latest query. Keep search, sort, paging, and column filters. Show an empty-table message when no successful batch exists.

- [ ] **Step 8: Run controller tests and Razor build**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~StockUploadControllerTests|FullyQualifiedName~StockSnapshotQueryServiceTests"
dotnet build GiselX.Web\GiselX.Web.csproj --no-restore -p:BaseOutputPath=.tmp\stock-ui\bin\
```

Expected: tests pass and Razor compiles.

- [ ] **Step 9: Commit UI**

```powershell
git commit -m "feat: inspect and browse stock snapshots"
```

---

### Task 8: Full verification and supplied distributor workbook acceptance

**Files:**
- Modify only when a verification-discovered defect first receives a failing regression test.

**Interfaces:**
- Consumes the complete feature through tests, migration inspection, and the running MVC application.

- [ ] **Step 1: Run focused Stock and mapping tests**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~Stock|FullyQualifiedName~TemplateMapping"
```

Expected: all focused tests pass.

- [ ] **Step 2: Run the complete suite using isolated output**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\stock-final\bin\ --verbosity minimal
```

Expected: zero failures. Report existing NuGet advisory warnings separately; do not accept compilation or test failures.

- [ ] **Step 3: Inspect the migration/script**

```powershell
dotnet ef migrations script 20260810120000_AddMultiWeekSelloutUploadGroup AddStockSnapshotUploads --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext --no-build
```

Verify the script contains no `DELETE`, no destructive rewrite of `Stock`, and only nullable additions to the existing table.

- [ ] **Step 4: Browser-check a real distributor Stock workbook**

Use an administrator-configured Stock mapping and a workbook containing at least one valid row, one `SaldoAkhir = 0` row, one negative `SaldoAkhir` row, and a trailing summary. Verify preflight counts, cancel/no-write behavior, confirmation, staging of every data row, partial final publication, warning result, and latest table contents.

- [ ] **Step 5: Verify immutable history and downloads**

Upload a second workbook for the same StockDate. Verify both batches and both final row sets remain in SQL, the main table shows only the second completed batch, the dropdown lists both with upload time/StockDate/filename, and selecting each exports only its linked valid final rows.

- [ ] **Step 6: Verify failure preservation**

Upload a workbook with zero valid rows. Verify the failed batch and staging diagnostics exist, no final rows link to it, it does not appear in the download dropdown, and the previous successful batch remains visible.

- [ ] **Step 7: Review final workspace changes**

When Git is available:

```powershell
git diff --check
git status --short
```

Confirm no temporary probe tests, generated workbooks, uploaded files, or isolated `.tmp` build outputs are included in the change set.
