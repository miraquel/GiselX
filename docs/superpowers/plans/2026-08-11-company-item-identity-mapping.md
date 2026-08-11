# Company Item Identity Mapping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let distributor companies upload their own item IDs/names for Stock and Sales while storing GMK identity and presenting the correct identity according to the authenticated user's `Distributor` role.

**Architecture:** Add a company-scoped `ItemMapping` aggregate and persist both GMK and distributor identities on staging and final rows. A shared resolver enriches parsed Stock and Sales rows during analysis; separate administration services handle CRUD and preview/confirmed Excel upserts. Query boundaries select GMK or distributor identity using a server-owned role flag while preserving existing company authorization.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC/Identity, Entity Framework Core SQL Server migrations, Dapper, ClosedXML 0.105.0, xUnit 2.9.3.

## Global Constraints

- `Company.ItemMappingEnabled` is `false` by default and mapping affects future uploads only.
- All four mapping values are required: GMK Item ID/Name and Distributor Item ID/Name.
- Match on trimmed, case-insensitive Company + Distributor Item ID only; names never control matching.
- Preserve the original uploaded distributor ID/name and the mapping ID on staged and final records.
- Stock excludes unmapped rows with warnings; Sales rejects a group containing any unmapped row.
- A user in the exact `Distributor` role sees distributor identity; every other user sees GMK identity; Distributor wins for multi-role users.
- Role-aware identity applies to grids, API results, filters, sorting, and Excel downloads without broadening company access.
- Use separate activate/deactivate POST actions and retain existing mapping activation state during Excel updates.
- Follow strict red-green-refactor: every production behavior starts with a focused test that is observed failing for the expected reason.
- Preserve unrelated dirty-worktree changes and stage only files belonging to the current task.

---

### Task 1: Persist the item crosswalk and dual identities

**Files:**
- Create: `GiselX.Domain/ItemMapping.cs`
- Create: `GiselX.Domain/ItemMappingDeleteResult.cs`
- Modify: `GiselX.Domain/Company.cs`
- Modify: `GiselX.Domain/Stock.cs`
- Modify: `GiselX.Domain/StockStaging.cs`
- Modify: `GiselX.Domain/SalesTransaction.cs`
- Modify: `GiselX.Domain/SelloutStaging.cs`
- Modify: `GiselX.Web/Context/GiselXDbContext.cs`
- Test: `GiselX.Tests/ItemMappingPersistenceModelTests.cs`

**Interfaces:**
- Produces: `ItemMapping` with `Id`, `CompanyId`, four required identity strings, `IsActive`, and audit fields.
- Produces: nullable `DistributorProductId`, `DistributorProductName`, and `ItemMappingId` properties on both staging and final transaction models.
- Produces: `Company.ItemMappingEnabled` and `Company.ItemMappings`.

- [ ] **Step 1: Write the failing EF model tests**

```csharp
[Fact]
public void Model_requires_company_scoped_unique_distributor_item_id()
{
    using var db = CreateContext();
    var mapping = db.Model.FindEntityType(typeof(ItemMapping))!;
    var index = Assert.Single(mapping.GetIndexes(), index => index.IsUnique);
    Assert.Equal([nameof(ItemMapping.CompanyId), nameof(ItemMapping.DistributorItemId)],
        index.Properties.Select(property => property.Name));
    Assert.Equal(DeleteBehavior.Restrict, Assert.Single(mapping.GetForeignKeys()).DeleteBehavior);
}

[Fact]
public void Model_persists_nullable_dual_identity_and_mapping_references()
{
    using var db = CreateContext();
    foreach (var type in new[] { typeof(Stock), typeof(StockStaging), typeof(SalesTransaction), typeof(SelloutStaging) })
    {
        var entity = db.Model.FindEntityType(type)!;
        Assert.True(entity.FindProperty("DistributorProductId")!.IsNullable);
        Assert.True(entity.FindProperty("DistributorProductName")!.IsNullable);
        Assert.True(entity.FindProperty("ItemMappingId")!.IsNullable);
    }
}
```

- [ ] **Step 2: Run the model tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingPersistenceModelTests`

Expected: FAIL because `ItemMapping` and the new model properties do not exist.

- [ ] **Step 3: Add the minimal domain and EF configuration**

```csharp
public sealed class ItemMapping
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string GmkItemId { get; set; } = string.Empty;
    public string GmkItemName { get; set; } = string.Empty;
    public string DistributorItemId { get; set; } = string.Empty;
    public string DistributorItemName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public Company Company { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = [];
    public ICollection<SalesTransaction> SalesTransactions { get; set; } = [];
}
```

Configure max lengths 100/200, audit username length 256, the unique `(CompanyId, DistributorItemId)` index, and `DeleteBehavior.Restrict` relationships. Configure `ItemMappingEnabled` default false.

- [ ] **Step 4: Run the focused model tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingPersistenceModelTests`

Expected: PASS.

- [ ] **Step 5: Commit the persistence model**

```powershell
git add GiselX.Domain/ItemMapping.cs GiselX.Domain/ItemMappingDeleteResult.cs GiselX.Domain/Company.cs GiselX.Domain/Stock.cs GiselX.Domain/StockStaging.cs GiselX.Domain/SalesTransaction.cs GiselX.Domain/SelloutStaging.cs GiselX.Web/Context/GiselXDbContext.cs GiselX.Tests/ItemMappingPersistenceModelTests.cs
git commit -m "feat: add company item mapping persistence model"
```

### Task 2: Add manual mapping administration and company enablement

**Files:**
- Create: `GiselX.Repository.Interface/IItemMappingRepository.cs`
- Create: `GiselX.Repository/ItemMappingRepository.cs`
- Create: `GiselX.Service.Dto/ItemMappingDto.cs`
- Create: `GiselX.Service.Dto/SaveItemMappingDto.cs`
- Create: `GiselX.Service.Interface/IItemMappingService.cs`
- Create: `GiselX.Service/ItemMappingService.cs`
- Modify: `GiselX.Repository/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Repository/CompanyRepository.cs`
- Modify: `GiselX.Service/CompanyService.cs`
- Modify: `GiselX.Service.Dto/CompanyDto.cs`
- Modify: `GiselX.Web/StoredProcedures/Company_Insert.sql`
- Modify: `GiselX.Web/StoredProcedures/Company_Update.sql`
- Modify: `GiselX.Web/StoredProcedures/Company_Select.sql`
- Modify: `GiselX.Web/StoredProcedures/Company_SelectById.sql`
- Test: `GiselX.Tests/ItemMappingServiceTests.cs`
- Test: `GiselX.Tests/CompanyItemMappingEnablementTests.cs`

**Interfaces:**
- Produces: `IItemMappingRepository.GetByCompanyAsync`, `GetActiveByCompanyAsync`, `GetByIdAsync`, `InsertAsync`, `UpdateAsync`, `SetActiveAsync`, `DeleteAsync`, and `UpsertAsync`.
- Produces: `IItemMappingService.ListAsync`, `GetAsync`, `SaveAsync`, `SetActiveAsync`, and `DeleteAsync`.
- Produces: `ICompanyService.UpdateCompanyAsync` rejection when `ItemMappingEnabled=true` and the company has no active mapping.

- [ ] **Step 1: Write failing service tests for required fields, normalized duplicates, audit, ownership, activation, and deletion**

```csharp
[Fact]
public async Task SaveAsync_rejects_trimmed_case_insensitive_duplicate()
{
    var repository = new FakeItemMappingRepository(new ItemMapping
        { Id = 3, CompanyId = 7, DistributorItemId = " p0003 " });
    var service = CreateService(repository);
    var dto = ValidSave();
    dto.DistributorItemId = "P0003";
    var response = await service.SaveAsync(dto, "admin", default);
    Assert.False(response.IsSuccess);
    Assert.Contains(response.Errors, error => error.Contains("Distributor Item ID"));
    Assert.Empty(repository.Inserted);
}

[Fact]
public async Task SaveAsync_trims_all_values_and_commits()
{
    var (service, repository, transaction) = CreateService();
    var response = await service.SaveAsync(ValidSave(), "admin", default);
    var saved = Assert.Single(repository.Inserted);
    Assert.Equal("4100039", saved.GmkItemId);
    Assert.Equal("P0003", saved.DistributorItemId);
    Assert.True(transaction.CommitCalled);
}
```

- [ ] **Step 2: Run the item mapping service tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingServiceTests`

Expected: FAIL because the repository/service contracts do not exist.

- [ ] **Step 3: Implement repository, DTOs, service validation, activation, and safe deletion**

Use this contract:

```csharp
public interface IItemMappingService
{
    Task<ServiceResponse<IReadOnlyList<ItemMappingDto>>> ListAsync(int companyId, CancellationToken token);
    Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(SaveItemMappingDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token);
}
```

Deletion returns `HasUploadHistory` when Stock, StockStaging, SalesTransaction, or SelloutStaging references the mapping.

- [ ] **Step 4: Run the focused service tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingServiceTests`

Expected: PASS.

- [ ] **Step 5: Write the failing company enablement tests**

```csharp
[Fact]
public async Task UpdateCompanyAsync_rejects_enablement_without_active_mapping()
{
    var response = await service.UpdateCompanyAsync(new CompanyDto { Id = 7, Name = "Distributor", ItemMappingEnabled = true }, default);
    Assert.False(response.IsSuccess);
    Assert.Contains(response.Errors, error => error.Contains("active item mapping"));
    Assert.False(transaction.CommitCalled);
}
```

- [ ] **Step 6: Run enablement tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~CompanyItemMappingEnablementTests`

Expected: FAIL because company update does not validate active mappings.

- [ ] **Step 7: Add `ItemMappingEnabled` to company DTO/repository procedures and enforce enablement in `CompanyService`**

Inject `IItemMappingRepository` into `CompanyService`. When a transition requests enabled state, call `GetActiveByCompanyAsync`; return HTTP 400 with `At least one active item mapping is required before item mapping can be enabled.` if none exist.

- [ ] **Step 8: Run both focused suites and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~ItemMappingServiceTests|FullyQualifiedName~CompanyItemMappingEnablementTests"`

Expected: PASS.

- [ ] **Step 9: Commit manual administration services**

```powershell
git add GiselX.Repository.Interface/IItemMappingRepository.cs GiselX.Repository/ItemMappingRepository.cs GiselX.Service.Dto/ItemMappingDto.cs GiselX.Service.Dto/SaveItemMappingDto.cs GiselX.Service.Interface/IItemMappingService.cs GiselX.Service/ItemMappingService.cs GiselX.Repository/ServiceCollectionExtensions.cs GiselX.Service/ServiceCollectionExtensions.cs GiselX.Repository/CompanyRepository.cs GiselX.Service/CompanyService.cs GiselX.Service.Dto/CompanyDto.cs GiselX.Web/StoredProcedures/Company_Insert.sql GiselX.Web/StoredProcedures/Company_Update.sql GiselX.Web/StoredProcedures/Company_Select.sql GiselX.Web/StoredProcedures/Company_SelectById.sql GiselX.Tests/ItemMappingServiceTests.cs GiselX.Tests/CompanyItemMappingEnablementTests.cs
git commit -m "feat: manage company item mappings"
```

### Task 3: Add transactional Excel preview and confirmed upsert

**Files:**
- Create: `GiselX.Service.Dto/ItemMappingImportErrorDto.cs`
- Create: `GiselX.Service.Dto/ItemMappingImportPreviewDto.cs`
- Create: `GiselX.Service.Dto/ItemMappingImportResultDto.cs`
- Create: `GiselX.Service.Interface/IItemMappingImportService.cs`
- Create: `GiselX.Service/ItemMappingWorkbookParser.cs`
- Create: `GiselX.Service/ItemMappingImportService.cs`
- Modify: `GiselX.Service/GiselX.Service.csproj`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Test: `GiselX.Tests/ItemMappingWorkbookParserTests.cs`
- Test: `GiselX.Tests/ItemMappingImportServiceTests.cs`

**Interfaces:**
- Produces: `IItemMappingImportService.PreflightAsync(byte[] workbook, int companyId, CancellationToken)`.
- Produces: `IItemMappingImportService.ImportAsync(byte[] workbook, int companyId, bool confirmed, string username, CancellationToken)`.
- Consumes: `IItemMappingRepository.UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken)` from Task 2.

- [ ] **Step 1: Write failing parser tests using a real in-memory workbook**

```csharp
[Fact]
public void Parse_reports_duplicate_normalized_distributor_ids()
{
    var bytes = Workbook(
        ("4100039", "Cream Caramel", " P0003 ", "Caramel Mix"),
        ("4100040", "Other", "p0003", "Other Name"));
    var result = new ItemMappingWorkbookParser().Parse(bytes);
    Assert.Equal(2, result.InvalidRows);
    Assert.All(result.Errors, error => Assert.Contains("duplicate", error.Reason, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run parser tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingWorkbookParserTests`

Expected: FAIL because the parser does not exist.

- [ ] **Step 3: Add ClosedXML to the Service project and implement exact-header parsing**

Reference ClosedXML `0.105.0`. Require the first worksheet headers `GMK Item ID`, `GMK Item Name`, `Distributor Item ID`, and `Distributor Item Name`; ignore fully blank trailing rows; preserve worksheet row numbers in errors; reject missing values, over-length values, and normalized duplicates.

- [ ] **Step 4: Run parser tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingWorkbookParserTests`

Expected: PASS.

- [ ] **Step 5: Write failing import-service tests for counts, confirmation, all-or-nothing validation, and active-state retention**

```csharp
[Fact]
public async Task ImportAsync_updates_existing_without_reactivating_and_inserts_new_active()
{
    repository.Mappings.Add(new ItemMapping { Id = 8, CompanyId = 7, DistributorItemId = "P0003", IsActive = false });
    var response = await service.ImportAsync(ValidWorkbookWithExistingAndNew(), 7, true, "admin", default);
    Assert.True(response.IsSuccess);
    Assert.False(repository.Upserted.Single(x => x.DistributorItemId == "P0003").IsActive);
    Assert.True(repository.Upserted.Single(x => x.DistributorItemId == "P0004").IsActive);
    Assert.True(transaction.CommitCalled);
}
```

- [ ] **Step 6: Run import-service tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingImportServiceTests`

Expected: FAIL because the import service does not exist.

- [ ] **Step 7: Implement preview classification and confirmed transactional upsert**

Return counts for `NewRows`, `UpdatedRows`, `UnchangedRows`, and `InvalidRows`. Reject unconfirmed imports. If any parsed row is invalid, return HTTP 400 and do not call `UpsertAsync` or commit. Compare all four trimmed values for unchanged detection. Preserve `Id`, `IsActive`, and creation audit for existing mappings; new mappings start active.

- [ ] **Step 8: Run parser and import tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~ItemMappingWorkbookParserTests|FullyQualifiedName~ItemMappingImportServiceTests"`

Expected: PASS.

- [ ] **Step 9: Commit Excel import behavior**

```powershell
git add GiselX.Service.Dto/ItemMappingImportErrorDto.cs GiselX.Service.Dto/ItemMappingImportPreviewDto.cs GiselX.Service.Dto/ItemMappingImportResultDto.cs GiselX.Service.Interface/IItemMappingImportService.cs GiselX.Service/ItemMappingWorkbookParser.cs GiselX.Service/ItemMappingImportService.cs GiselX.Service/GiselX.Service.csproj GiselX.Service/ServiceCollectionExtensions.cs GiselX.Tests/ItemMappingWorkbookParserTests.cs GiselX.Tests/ItemMappingImportServiceTests.cs
git commit -m "feat: import item mappings from excel"
```

### Task 4: Resolve item identity during Stock and Sales workbook analysis

**Files:**
- Create: `GiselX.Service.Dto/ItemMappingContextDto.cs`
- Create: `GiselX.Service.Dto/ItemIdentityResolutionDto.cs`
- Create: `GiselX.Service.Interface/IItemMappingResolver.cs`
- Create: `GiselX.Service/ItemMappingResolver.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Service/StockWorkbookAnalysisService.cs`
- Modify: `GiselX.Service/SelloutWorkbookAnalysisService.cs`
- Modify: `GiselX.Service.Dto/StockStagingRowDto.cs`
- Modify: `GiselX.Service/StockUploadService.cs`
- Modify: `GiselX.Repository/StockUploadRepository.cs`
- Modify: `GiselX.Repository/SelloutUploadRepository.cs`
- Test: `GiselX.Tests/ItemMappingResolverTests.cs`
- Test: `GiselX.Tests/StockWorkbookAnalysisServiceTests.cs`
- Test: `GiselX.Tests/SelloutWorkbookAnalysisServiceTests.cs`
- Test: `GiselX.Tests/StockUploadServiceTests.cs`
- Test: `GiselX.Tests/SelloutUploadServiceTests.cs`

**Interfaces:**
- Produces: `IItemMappingResolver.LoadAsync(int companyId, CancellationToken)` and `Resolve(ItemMappingContextDto, string?, string?)`.
- Produces: `ItemIdentityResolutionDto` containing distributor values, nullable GMK values, nullable mapping ID, and nullable error reason.
- Consumes: `ICompanyRepository.GetCompanyByIdAsync` for `ItemMappingEnabled` and `IItemMappingRepository.GetActiveByCompanyAsync` for the company catalog from Tasks 1-2.

- [ ] **Step 1: Write failing resolver tests for disabled, mapped, missing, inactive, casing, and whitespace behavior**

```csharp
[Fact]
public void Resolve_matches_trimmed_id_case_insensitively_and_ignores_name_difference()
{
    var context = EnabledContext(new ItemMappingDto
        { Id = 9, DistributorItemId = "P0003", DistributorItemName = "Saved Name", GmkItemId = "4100039", GmkItemName = "GMK Name", IsActive = true });
    var result = resolver.Resolve(context, " p0003 ", "Uploaded Different Name");
    Assert.True(result.IsMapped);
    Assert.Equal("4100039", result.GmkItemId);
    Assert.Equal("Uploaded Different Name", result.DistributorItemName);
    Assert.Null(result.ErrorReason);
}
```

- [ ] **Step 2: Run resolver tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingResolverTests`

Expected: FAIL because the resolver does not exist.

- [ ] **Step 3: Implement one-load-per-analysis resolver behavior**

Use `StringComparer.OrdinalIgnoreCase` over trimmed IDs. Disabled context returns the trimmed uploaded values as both GMK and distributor identity with null mapping ID. Enabled context returns `Distributor Item ID "{id}" has no active GMK item mapping.` when not found.

- [ ] **Step 4: Run resolver tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingResolverTests`

Expected: PASS.

- [ ] **Step 5: Add failing Stock and Sales analysis tests**

Extend the existing real-workbook analysis tests to assert that a mapped row receives GMK `ProductId/ProductName`, preserves `DistributorProductId/Name`, and stores `ItemMappingId`. Add an unmapped case asserting `ProductId` row errors, Stock invalid-row counts, and `SelloutWorkbookAnalysisDto.IsValid == false`.

- [ ] **Step 6: Run analysis tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~StockWorkbookAnalysisServiceTests|FullyQualifiedName~SelloutWorkbookAnalysisServiceTests"`

Expected: FAIL because analysis does not resolve item identity.

- [ ] **Step 7: Enrich parsed rows and merge mapping errors into each row's JSON and analysis error list**

Load the mapping context once after workbook-template detection succeeds. For each row, copy distributor fields first, then set GMK fields and mapping ID or mark the row invalid with a `ProductId` error using its source row number. Do not require the uploaded name to equal the saved distributor name.

- [ ] **Step 8: Add failing upload persistence assertions**

In the upload-service tests, capture staging rows and assert both identities and mapping ID are forwarded. Assert Stock publishes remaining mapped rows but Sales does not call promotion when any mapping error exists.

- [ ] **Step 9: Run upload tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~StockUploadServiceTests|FullyQualifiedName~SelloutUploadServiceTests"`

Expected: FAIL because staging and promotion SQL do not include dual identity.

- [ ] **Step 10: Update staging inserts and final promotion SQL**

Insert distributor fields and mapping ID into both staging tables. Promote GMK `ProductId/ProductName`, distributor fields, and mapping ID into final Stock/Sales rows. Keep current Stock valid-row filtering and Sales group atomicity.

- [ ] **Step 11: Run all resolver, analysis, and upload tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~ItemMappingResolverTests|FullyQualifiedName~StockWorkbookAnalysisServiceTests|FullyQualifiedName~SelloutWorkbookAnalysisServiceTests|FullyQualifiedName~StockUploadServiceTests|FullyQualifiedName~SelloutUploadServiceTests"`

Expected: PASS.

- [ ] **Step 12: Commit upload resolution**

```powershell
git add GiselX.Service.Dto/ItemMappingContextDto.cs GiselX.Service.Dto/ItemIdentityResolutionDto.cs GiselX.Service.Interface/IItemMappingResolver.cs GiselX.Service/ItemMappingResolver.cs GiselX.Service/ServiceCollectionExtensions.cs GiselX.Service/StockWorkbookAnalysisService.cs GiselX.Service/SelloutWorkbookAnalysisService.cs GiselX.Service.Dto/StockStagingRowDto.cs GiselX.Service/StockUploadService.cs GiselX.Repository/StockUploadRepository.cs GiselX.Repository/SelloutUploadRepository.cs GiselX.Tests/ItemMappingResolverTests.cs GiselX.Tests/StockWorkbookAnalysisServiceTests.cs GiselX.Tests/SelloutWorkbookAnalysisServiceTests.cs GiselX.Tests/StockUploadServiceTests.cs GiselX.Tests/SelloutUploadServiceTests.cs
git commit -m "feat: resolve mapped item identity during uploads"
```

### Task 5: Build the GMK item-mapping administration UI

**Files:**
- Create: `GiselX.Web/Controllers/ItemMappingsController.cs`
- Create: `GiselX.Web/Models/ItemMappingListViewModel.cs`
- Create: `GiselX.Web/Models/ItemMappingEditViewModel.cs`
- Create: `GiselX.Web/Areas/Admin/Views/ItemMappings/Index.cshtml`
- Create: `GiselX.Web/Areas/Admin/Views/ItemMappings/Edit.cshtml`
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml`
- Modify: `GiselX.Web/Controllers/CompaniesController.cs`
- Test: `GiselX.Tests/ItemMappingControllerTests.cs`

**Interfaces:**
- Consumes: CRUD and import services from Tasks 2-3.
- Produces: Company Edit enable switch/link, manual mapping forms, activation commands, `.xlsx` template, preview, and confirmed import endpoints.

- [ ] **Step 1: Write failing controller tests for ownership routing, activate/deactivate constants, manual save, template headers, preview, and confirmed import**

```csharp
[Fact]
public async Task Activate_forwards_server_owned_true_state()
{
    var controller = CreateController();
    var result = await controller.Activate(9, 7, default);
    Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal((9, 7, true, "admin"), service.ActiveChange);
}

[Fact]
public void DownloadTemplate_contains_the_four_required_headers()
{
    var file = Assert.IsType<FileContentResult>(controller.DownloadTemplate(7));
    using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
    Assert.Equal("GMK Item ID", workbook.Worksheet(1).Cell(1, 1).GetString());
    Assert.Equal("Distributor Item Name", workbook.Worksheet(1).Cell(1, 4).GetString());
}
```

- [ ] **Step 2: Run controller tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingControllerTests`

Expected: FAIL because the controller and view models do not exist.

- [ ] **Step 3: Implement the authorized controller and view models**

Apply `[Area("Admin")]` and `[Authorize(PermissionConstants.Companies.Edit)]`. Use separate POST `Activate` and `Deactivate` actions with anti-forgery. `PreflightImport` and `Import` accept the same browser-selected file; confirmed import must pass `confirmed=true` server-side only after the UI confirmation.

- [ ] **Step 4: Run controller tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~ItemMappingControllerTests`

Expected: PASS.

- [ ] **Step 5: Add list/edit/import Razor views and Company Edit controls**

Render the five requested table columns (including No), search box, sortable headers, status badge, and actions. Add a four-field required edit form. Add a file picker whose JavaScript posts preflight, renders new/updated/unchanged/invalid counts plus row errors, asks for confirmation, then posts the same file to confirmed import. Add `ItemMappingEnabled` checkbox and `Manage Item Mappings` link to Company Edit.

- [ ] **Step 6: Build the Web project to compile Razor views**

Run: `dotnet build GiselX.Web/GiselX.Web.csproj --no-restore`

Expected: PASS with zero errors.

- [ ] **Step 7: Commit administration UI**

```powershell
git add GiselX.Web/Controllers/ItemMappingsController.cs GiselX.Web/Models/ItemMappingListViewModel.cs GiselX.Web/Models/ItemMappingEditViewModel.cs GiselX.Web/Areas/Admin/Views/ItemMappings/Index.cshtml GiselX.Web/Areas/Admin/Views/ItemMappings/Edit.cshtml GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml GiselX.Web/Controllers/CompaniesController.cs GiselX.Tests/ItemMappingControllerTests.cs
git commit -m "feat: add item mapping administration UI"
```

### Task 6: Present distributor identity in Stock for Distributor-role users

**Files:**
- Modify: `GiselX.Repository.Interface/IStockRepository.cs`
- Modify: `GiselX.Repository/StockRepository.cs`
- Modify: `GiselX.Service.Interface/IStockService.cs`
- Modify: `GiselX.Service/StockService.cs`
- Modify: `GiselX.Web/Controllers/Api/StocksController.cs`
- Modify: `GiselX.Web/Controllers/StocksController.cs`
- Test: `GiselX.Tests/StockRoleAwareIdentityTests.cs`
- Test: `GiselX.Tests/StockDownloadControllerTests.cs`

**Interfaces:**
- Produces: `SelectAsync(PagedListRequestDto request, bool useDistributorIdentity, CancellationToken)` at the service boundary.
- Produces: repository query methods accepting `useDistributorIdentity` for list/filter/sort/download paths.
- Consumes: `User.IsInRole("Distributor")` in API and MVC controllers.

- [ ] **Step 1: Write failing query/service tests for GMK and distributor output, filter, search, and sort expressions**

```csharp
[Fact]
public async Task SelectAsync_uses_distributor_columns_for_product_filter_and_sort_when_requested()
{
    await repository.SelectAsync(Request(filter: ("ProductId", "P0003"), sort: "ProductName"), 7, true, default);
    Assert.Contains("DistributorProductId", connection.LastCommandText);
    Assert.Contains("DistributorProductName", connection.LastCommandText);
}
```

- [ ] **Step 2: Run Stock role-aware tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~StockRoleAwareIdentityTests`

Expected: FAIL because the role flag and distributor-column query expressions do not exist.

- [ ] **Step 3: Add the server-owned identity flag through controller, service, and repository**

For distributor mode use `COALESCE(NULLIF(s.DistributorProductId,''),s.ProductId)` and the equivalent name expression for SELECT aliases, search, ProductId/ProductName filters, and sorting. GMK mode uses existing fields. Keep all SQL identifiers from a fixed whitelist.

- [ ] **Step 4: Extend download tests and implement role-aware batch export**

Assert a Distributor-role principal receives distributor Product ID/Name in the generated workbook and a non-Distributor principal receives GMK values. The controller passes only `User.IsInRole("Distributor")`; it never accepts this flag from request data.

- [ ] **Step 5: Run Stock focused tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~StockRoleAwareIdentityTests|FullyQualifiedName~StockDownloadControllerTests"`

Expected: PASS.

- [ ] **Step 6: Commit Stock presentation**

```powershell
git add GiselX.Repository.Interface/IStockRepository.cs GiselX.Repository/StockRepository.cs GiselX.Service.Interface/IStockService.cs GiselX.Service/StockService.cs GiselX.Web/Controllers/Api/StocksController.cs GiselX.Web/Controllers/StocksController.cs GiselX.Tests/StockRoleAwareIdentityTests.cs GiselX.Tests/StockDownloadControllerTests.cs
git commit -m "feat: show role-aware item identity in stock"
```

### Task 7: Present distributor identity in Sales for Distributor-role users

**Files:**
- Modify: `GiselX.Repository.Interface/ISalesTransactionRepository.cs`
- Modify: `GiselX.Repository/SalesTransactionRepository.cs`
- Modify: `GiselX.Service.Interface/ISalesTransactionService.cs`
- Modify: `GiselX.Service/SalesTransactionService.cs`
- Modify: `GiselX.Web/Controllers/Api/SalesTransactionsController.cs`
- Modify: `GiselX.Web/Controllers/SalesTransactionsController.cs`
- Modify: `GiselX.Web/StoredProcedures/GiselSales_Select.sql`
- Create: `GiselX.Web/StoredProcedures/GiselSales_SelectByCustPeriod.sql`
- Test: `GiselX.Tests/SalesRoleAwareIdentityTests.cs`
- Test: `GiselX.Tests/SalesTransactionDownloadControllerTests.cs`

**Interfaces:**
- Produces: role-aware Sales select and period-download signatures parallel to Task 6.
- Consumes: `User.IsInRole("Distributor")` in Sales API and MVC controllers.

- [ ] **Step 1: Write failing repository contract tests for the `@UseDistributorIdentity` parameter and stored-procedure behavior**

```csharp
[Fact]
public async Task SelectAsync_passes_distributor_identity_mode_to_sql()
{
    await repository.SelectAsync(Request(), 7, true, default);
    Assert.True(connection.Parameters.Get<bool>("UseDistributorIdentity"));
}
```

Add a repository boundary test that captures the command parameters and a LocalDB integration test that installs the procedure into a uniquely named temporary database, inserts one row containing distinct GMK/distributor identities, executes both role modes, asserts the selected pair, and drops the temporary database in test cleanup.

- [ ] **Step 2: Run Sales role-aware tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~SalesRoleAwareIdentityTests`

Expected: FAIL because the new parameter is absent.

- [ ] **Step 3: Thread role mode through controllers/services/repository and update procedures**

Add `@UseDistributorIdentity bit = 0`. Alias the selected Product ID/Name with `CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF(DistributorProductId,''),ProductId) ELSE ProductId END`. Use the same expression for ProductId/ProductName filters and sort cases. Apply identical output behavior to the period download procedure.

- [ ] **Step 4: Add failing controller download tests for both role modes**

Build principals with and without `ClaimTypes.Role = "Distributor"`; assert the service receives the correct server-owned flag and the resulting workbook contains the corresponding ID/name pair.

- [ ] **Step 5: Run Sales focused tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~SalesRoleAwareIdentityTests|FullyQualifiedName~SalesTransactionDownloadControllerTests"`

Expected: PASS.

- [ ] **Step 6: Commit Sales presentation**

```powershell
git add GiselX.Repository.Interface/ISalesTransactionRepository.cs GiselX.Repository/SalesTransactionRepository.cs GiselX.Service.Interface/ISalesTransactionService.cs GiselX.Service/SalesTransactionService.cs GiselX.Web/Controllers/Api/SalesTransactionsController.cs GiselX.Web/Controllers/SalesTransactionsController.cs GiselX.Web/StoredProcedures/GiselSales_Select.sql GiselX.Web/StoredProcedures/GiselSales_SelectByCustPeriod.sql GiselX.Tests/SalesRoleAwareIdentityTests.cs GiselX.Tests/SalesTransactionDownloadControllerTests.cs
git commit -m "feat: show role-aware item identity in sales"
```

### Task 8: Add the deployable migration and manual SQL parity

**Files:**
- Create: `GiselX.Web/Context/Migrations/20260811180000_AddCompanyItemIdentityMapping.cs`
- Modify: `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`
- Create: `GiselX.Web/DatabaseScripts/20260811180000_AddCompanyItemIdentityMapping.sql`
- Modify: `GiselX.Web/Stock.sql`
- Modify: `GiselX.Web/SalesTransaction.sql`
- Test: `GiselX.Tests/ItemMappingMigrationTests.cs`
- Test: `GiselX.Tests/StockManualSqlScriptTests.cs`
- Create: `GiselX.Tests/SalesManualSqlScriptTests.cs`

**Interfaces:**
- Consumes: final EF model and stored procedure definitions from Tasks 1-7.
- Produces: an idempotent-safe schema upgrade, historical identity backfill, and procedure updates.

- [ ] **Step 1: Write failing migration/model/script tests**

```csharp
[Fact]
public void Migration_backfills_distributor_identity_without_assigning_a_mapping()
{
    var sql = ReadMigrationSql();
    Assert.Contains("DistributorProductId = ProductId", sql);
    Assert.Contains("DistributorProductName = ProductName", sql);
    Assert.DoesNotContain("SET ProductId = DistributorProductId", sql, StringComparison.OrdinalIgnoreCase);
}
```

Model assertions must require `Company.ItemMappingEnabled` default false, the unique mapping index, max lengths, nullable historical FKs, and Restrict delete behavior. Script behavior tests create a uniquely named temporary LocalDB database, run the manual script twice, inspect resulting column types/nullability/constraints, and drop the database in test cleanup.

- [ ] **Step 2: Run migration/script tests and verify RED**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~ItemMappingMigrationTests|FullyQualifiedName~StockManualSqlScriptTests|FullyQualifiedName~SalesManualSqlScriptTests"`

Expected: FAIL because migration and SQL artifacts do not contain the item-mapping schema.

- [ ] **Step 3: Generate and review the EF migration, then add explicit backfill and procedure SQL**

Run: `dotnet ef migrations add AddCompanyItemIdentityMapping --project GiselX.Web/GiselX.Web.csproj --startup-project GiselX.Web/GiselX.Web.csproj --output-dir Context/Migrations`

Ensure the migration creates `ItemMapping`, adds `ItemMappingEnabled default 0`, adds nullable dual-identity/mapping columns to both final and staging tables, backfills only final distributor fields from current product fields, and executes the role-aware Company/Sales procedure definitions. `Down` removes FKs before columns/table and restores prior procedure signatures.

- [ ] **Step 4: Update manual schema/query scripts to match the migration**

The manual script checks actual metadata before adding or altering objects, uses `nvarchar(100)`/`nvarchar(200)`, leaves `ItemMappingId` nullable, and never truncates transaction data.

- [ ] **Step 5: Run migration/script tests and verify GREEN**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~ItemMappingPersistenceModelTests|FullyQualifiedName~ItemMappingMigrationTests|FullyQualifiedName~StockManualSqlScriptTests|FullyQualifiedName~SalesManualSqlScriptTests"`

Expected: PASS.

- [ ] **Step 6: Commit deployable database changes**

```powershell
git add GiselX.Web/Context/Migrations GiselX.Web/DatabaseScripts/20260811180000_AddCompanyItemIdentityMapping.sql GiselX.Web/Stock.sql GiselX.Web/SalesTransaction.sql GiselX.Tests/ItemMappingMigrationTests.cs GiselX.Tests/StockManualSqlScriptTests.cs GiselX.Tests/SalesManualSqlScriptTests.cs
git commit -m "feat: migrate company item identity mappings"
```

### Task 9: Complete end-to-end regression verification

**Files:**
- Test: all `GiselX.Tests` tests; no new production files are planned in this task.

**Interfaces:**
- Consumes: the complete feature from Tasks 1-8.
- Produces: a buildable, migration-safe, fully tested implementation.

- [ ] **Step 1: Run focused feature tests together**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~ItemMapping|FullyQualifiedName~RoleAwareIdentity|FullyQualifiedName~StockUploadServiceTests|FullyQualifiedName~SelloutUploadServiceTests"`

Expected: PASS with zero failures.

- [ ] **Step 2: Build the complete solution**

Run: `dotnet build GiselX.sln --no-restore`

Expected: PASS with zero errors.

- [ ] **Step 3: Run the complete test suite**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --no-build`

Expected: PASS with zero failures.

- [ ] **Step 4: Verify migration SQL generation**

Run: `dotnet ef migrations script 20260810190000_AddStockPriceUnit 20260811180000_AddCompanyItemIdentityMapping --project GiselX.Web/GiselX.Web.csproj --startup-project GiselX.Web/GiselX.Web.csproj --no-build --output .tmp/item-mapping-migration.sql`

Expected: command succeeds; inspection confirms nullable `nvarchar(100/200)` distributor columns, nullable mapping FKs, default-false company flag, final-row backfill, and updated procedures.

- [ ] **Step 5: Check only intended files for whitespace and accidental changes**

Run: `git diff --check`

Run: `git status --short`

Expected: no whitespace errors; unrelated pre-existing changes remain unstaged and untouched.
