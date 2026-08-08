# Distributor Mapping Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dedicated per-company page for listing, editing, activating, deactivating, and safely deleting distributor sellout mappings.

**Architecture:** Extend the existing mapping repository and service with a guarded delete result that distinguishes deleted, missing, and upload-protected mappings. Add typed list-page models and controller endpoints, then render management actions through anti-forgery-protected forms. Keep upload history immutable and preload saved assignments in the existing editor.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor, Dapper, SQL Server, xUnit.

## Global Constraints

- A mapping referenced by any `SelloutUploadBatch` cannot be deleted.
- Delete never removes upload batches, staging rows, or sales transactions.
- Every mapping operation is constrained by both mapping ID and company ID.
- Activate, deactivate, and delete use POST with anti-forgery validation.
- All changes must preserve the existing distributor mapping and sellout upload behavior.

---

### Task 1: Guarded Mapping Deletion

**Files:**
- Create: `GiselX.Repository.Interface/DistributorTemplateMappingDeleteResult.cs`
- Modify: `GiselX.Repository.Interface/IDistributorTemplateMappingRepository.cs`
- Modify: `GiselX.Repository/DistributorTemplateMappingRepository.cs`
- Modify: `GiselX.Service.Interface/IDistributorTemplateMappingService.cs`
- Modify: `GiselX.Service/DistributorTemplateMappingService.cs`
- Test: `GiselX.Tests/DistributorTemplateMappingServiceTests.cs`

**Interfaces:**
- Produces: `DistributorTemplateMappingDeleteResult` with `NotFound`, `Deleted`, and `HasUploadHistory` values.
- Produces: `IDistributorTemplateMappingRepository.DeleteAsync(int id, int companyId, CancellationToken)`.
- Produces: `IDistributorTemplateMappingService.DeleteAsync(int id, int companyId, CancellationToken)`.

- [ ] **Step 1: Write failing service tests for successful and protected deletion**

Add repository delete-result control and these tests to `DistributorTemplateMappingServiceTests`:

```csharp
[Fact]
public async Task DeleteAsync_DeletesUnusedMappingAndCommits()
{
    var (service, repository, transaction) = CreateService();
    repository.DeleteResult = DistributorTemplateMappingDeleteResult.Deleted;

    var response = await service.DeleteAsync(7, 2, CancellationToken.None);

    Assert.True(response.IsSuccess);
    Assert.Equal((7, 2), repository.DeletedMapping);
    Assert.True(transaction.CommitCalled);
}

[Fact]
public async Task DeleteAsync_BlocksMappingWithUploadHistoryWithoutCommit()
{
    var (service, repository, transaction) = CreateService();
    repository.DeleteResult = DistributorTemplateMappingDeleteResult.HasUploadHistory;

    var response = await service.DeleteAsync(7, 2, CancellationToken.None);

    Assert.False(response.IsSuccess);
    Assert.Contains(response.Errors, error => error.Contains("upload history", StringComparison.OrdinalIgnoreCase));
    Assert.False(transaction.CommitCalled);
}

[Fact]
public async Task DeleteAsync_RejectsMappingFromAnotherCompanyWithoutCommit()
{
    var (service, repository, transaction) = CreateService();
    repository.DeleteResult = DistributorTemplateMappingDeleteResult.NotFound;

    var response = await service.DeleteAsync(7, 99, CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
    Assert.False(transaction.CommitCalled);
}
```

Update `FakeMappingRepository` so `DeleteAsync` captures `(id, companyId)` and returns a configured result.

- [ ] **Step 2: Run the deletion tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~DistributorTemplateMappingServiceTests.DeleteAsync" --no-restore
```

Expected: FAIL because the delete result and service/repository methods do not exist.

- [ ] **Step 3: Add the guarded delete contract and repository implementation**

Create:

```csharp
namespace GiselX.Repository.Interface;

public enum DistributorTemplateMappingDeleteResult
{
    NotFound,
    Deleted,
    HasUploadHistory
}
```

Add to `IDistributorTemplateMappingRepository`:

```csharp
Task<DistributorTemplateMappingDeleteResult> DeleteAsync(
    int id, int companyId, CancellationToken cancellationToken);
```

Implement a single guarded SQL batch in `DistributorTemplateMappingRepository`:

```csharp
public async Task<DistributorTemplateMappingDeleteResult> DeleteAsync(
    int id, int companyId, CancellationToken cancellationToken)
{
    const string sql = """
        DELETE mapping
        FROM DistributorTemplateMapping mapping
        WHERE mapping.Id = @Id
          AND mapping.CompanyId = @CompanyId
          AND NOT EXISTS (
              SELECT 1 FROM SelloutUploadBatch batch
              WHERE batch.DistributorTemplateMappingId = mapping.Id);

        SELECT CASE
            WHEN @@ROWCOUNT = 1 THEN 1
            WHEN EXISTS (
                SELECT 1 FROM DistributorTemplateMapping mapping
                INNER JOIN SelloutUploadBatch batch
                    ON batch.DistributorTemplateMappingId = mapping.Id
                WHERE mapping.Id = @Id AND mapping.CompanyId = @CompanyId)
                THEN 2
            ELSE 0
        END;
        """;
    var command = new CommandDefinition(sql, new { Id = id, CompanyId = companyId },
        _dbTransaction, cancellationToken: cancellationToken);
    return (DistributorTemplateMappingDeleteResult)
        await _dbConnection.ExecuteScalarAsync<int>(command);
}
```

- [ ] **Step 4: Implement service result handling**

Add to `IDistributorTemplateMappingService` and implement:

```csharp
public async Task<ServiceResponse> DeleteAsync(
    int id, int companyId, CancellationToken cancellationToken)
{
    var result = await _repository.DeleteAsync(id, companyId, cancellationToken);
    if (result == DistributorTemplateMappingDeleteResult.NotFound)
        return new ServiceResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Errors = ["The distributor template mapping was not found."]
        };
    if (result == DistributorTemplateMappingDeleteResult.HasUploadHistory)
        return new ServiceResponse
        {
            StatusCode = StatusCodes.Status409Conflict,
            Errors = ["This mapping has upload history and cannot be deleted. Deactivate it instead."]
        };

    _dbTransaction.Commit();
    return new ServiceResponse { Message = "Distributor template mapping deleted successfully." };
}
```

- [ ] **Step 5: Run the focused tests and verify GREEN**

Run the command from Step 2. Expected: all `DeleteAsync` tests pass.

- [ ] **Step 6: Commit Task 1**

```powershell
git add GiselX.Repository.Interface/DistributorTemplateMappingDeleteResult.cs GiselX.Repository.Interface/IDistributorTemplateMappingRepository.cs GiselX.Repository/DistributorTemplateMappingRepository.cs GiselX.Service.Interface/IDistributorTemplateMappingService.cs GiselX.Service/DistributorTemplateMappingService.cs GiselX.Tests/DistributorTemplateMappingServiceTests.cs
git commit -m "feat: safely delete unused distributor mappings"
```

---

### Task 2: Management Controller Endpoints

**Files:**
- Create: `GiselX.Web/Models/DistributorTemplateMappingListViewModel.cs`
- Modify: `GiselX.Web/Controllers/DistributorTemplateMappingsController.cs`
- Test: `GiselX.Tests/DistributorTemplateMappingControllerTests.cs`

**Interfaces:**
- Consumes: existing `ListAsync`, `SetActiveAsync`, and Task 1 `DeleteAsync` service methods.
- Produces: `Index`, `SetActive`, and `Delete` MVC actions.
- Produces: `DistributorTemplateMappingListViewModel` with `CompanyId` and `Mappings`.

- [ ] **Step 1: Write failing controller tests**

Add tests that assert:

```csharp
[Fact]
public async Task Index_ReturnsMappingsForRequestedCompany()
{
    var service = new CapturingMappingService
    {
        ListedMappings = [new DistributorTemplateMappingDto { Id = 7, CompanyId = 2, Name = "Weekly" }]
    };
    var controller = CreateController(service);

    var result = await controller.Index(2, CancellationToken.None);

    var view = Assert.IsType<ViewResult>(result);
    var model = Assert.IsType<DistributorTemplateMappingListViewModel>(view.Model);
    Assert.Equal(2, model.CompanyId);
    Assert.Single(model.Mappings);
}

[Fact]
public async Task Delete_RedirectsToCompanyMappingListWithServiceMessage()
{
    var service = new CapturingMappingService();
    var controller = CreateController(service);

    var result = await controller.Delete(7, 2, CancellationToken.None);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(DistributorTemplateMappingsController.Index), redirect.ActionName);
    Assert.Equal(2, redirect.RouteValues!["companyId"]);
    Assert.Equal((7, 2), service.DeletedMapping);
}
```

Add equivalent coverage for `SetActive`, including the requested Boolean value and authenticated username. Update the fake service with complete interface behavior and capture fields.

- [ ] **Step 2: Run the controller tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~DistributorTemplateMappingControllerTests" --no-restore
```

Expected: FAIL because the list model and actions do not exist.

- [ ] **Step 3: Add the typed list model**

Create:

```csharp
using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class DistributorTemplateMappingListViewModel
{
    public int CompanyId { get; set; }
    public IReadOnlyList<DistributorTemplateMappingDto> Mappings { get; set; } = [];
}
```

- [ ] **Step 4: Add Index and mutation actions**

Add `Index`, `SetActive`, and `Delete` actions. Mutation actions must be `[HttpPost, ValidateAntiForgeryToken]`, put success in `TempData["StatusMessage"]`, put joined errors in `TempData["ErrorMessage"]`, and redirect to `Index` with the same company ID.

Add `using GiselX.Service.Dto.Common;` to the controller for the shared response type.

Use this shared response handling shape without adding a new abstraction:

```csharp
private IActionResult RedirectToList(int companyId, ServiceResponse response)
{
    if (response.IsSuccess)
        TempData["StatusMessage"] = response.Message;
    else
        TempData["ErrorMessage"] = string.Join(" ", response.Errors);
    return RedirectToAction(nameof(Index), new { companyId });
}
```

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all controller tests pass.

- [ ] **Step 6: Commit Task 2**

```powershell
git add GiselX.Web/Models/DistributorTemplateMappingListViewModel.cs GiselX.Web/Controllers/DistributorTemplateMappingsController.cs GiselX.Tests/DistributorTemplateMappingControllerTests.cs
git commit -m "feat: add distributor mapping management endpoints"
```

---

### Task 3: Management Page and Existing Assignment Display

**Files:**
- Create: `GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Index.cshtml`
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml`
- Modify: `GiselX.Web/Models/DistributorTemplateMappingEditViewModel.cs`
- Modify: `GiselX.Web/Controllers/DistributorTemplateMappingsController.cs`
- Modify: `GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Edit.cshtml`
- Test: `GiselX.Tests/DistributorTemplateMappingControllerTests.cs`

**Interfaces:**
- Consumes: Task 2 list model and endpoints.
- Produces: server-rendered list with Edit, Activate/Deactivate, and Delete forms.
- Produces: `SourceHeader` on `DistributorTemplateColumnEditViewModel` for existing assignment display.

- [ ] **Step 1: Write a failing Edit controller test for stored assignments**

Configure the fake service to return a mapping with a stored column and assert:

```csharp
[Fact]
public async Task Edit_IncludesStoredSourceHeadersAndAssignments()
{
    var service = new CapturingMappingService
    {
        Mapping = new DistributorTemplateMappingDto
        {
            Id = 7,
            CompanyId = 2,
            Columns =
            [
                new DistributorTemplateColumnDto
                {
                    SourceColumnNumber = 4,
                    SourceHeader = "Invoice Number",
                    CanonicalField = SelloutCanonicalField.InvoiceNo
                }
            ]
        }
    };
    var controller = CreateController(service);

    var result = await controller.Edit(7, 2, CancellationToken.None);

    var view = Assert.IsType<ViewResult>(result);
    var model = Assert.IsType<DistributorTemplateMappingEditViewModel>(view.Model);
    var column = Assert.Single(model.Columns);
    Assert.Equal("Invoice Number", column.SourceHeader);
    Assert.Equal(SelloutCanonicalField.InvoiceNo, column.CanonicalField);
}
```

- [ ] **Step 2: Run the Edit test and verify RED**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~DistributorTemplateMappingControllerTests.Edit_IncludesStoredSourceHeadersAndAssignments" --no-restore
```

Expected: FAIL because the edit view model does not expose `SourceHeader`.

- [ ] **Step 3: Preserve source headers in the edit model**

Add `public string SourceHeader { get; set; } = string.Empty;` to `DistributorTemplateColumnEditViewModel` and map it in the controller's Edit action. Keep Save authoritative by replacing posted headers with the inspected workbook headers.

- [ ] **Step 4: Build the management Razor page**

Create `Index.cshtml` using `_ContentNavbarLayout`. Render status/error TempData alerts, Add Mapping and Back to Company links, an empty state, and a responsive table. Each row must use:

```html
<a asp-action="Edit" asp-route-id="@mapping.Id" asp-route-companyId="@Model.CompanyId"
   class="btn btn-sm btn-outline-primary">Edit</a>

<form asp-action="SetActive" method="post" class="d-inline">
    <input type="hidden" name="id" value="@mapping.Id" />
    <input type="hidden" name="companyId" value="@Model.CompanyId" />
    <input type="hidden" name="isActive" value="@(!mapping.IsActive)" />
    <button type="submit" class="btn btn-sm btn-outline-secondary">
        @(mapping.IsActive ? "Deactivate" : "Activate")
    </button>
</form>

<form asp-action="Delete" method="post" class="d-inline"
      onsubmit="return confirm('Permanently delete this unused mapping?');">
    <input type="hidden" name="id" value="@mapping.Id" />
    <input type="hidden" name="companyId" value="@Model.CompanyId" />
    <button type="submit" class="btn btn-sm btn-outline-danger">Delete</button>
</form>
```

Razor form tag helpers generate anti-forgery tokens automatically.

- [ ] **Step 5: Add Company navigation and render stored Edit rows**

Add a Manage Distributor Mappings link beside Add Mapping on Company Edit. In mapping `Edit.cshtml`, render `Model.Columns` into `<tbody>` with indexed hidden source-column inputs, encoded source headers, and nullable enum selects. Keep the Inspect button's JavaScript replacement behavior unchanged.

- [ ] **Step 6: Run focused tests and compile Razor views**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~DistributorTemplateMappingControllerTests" --no-restore
dotnet build GiselX.Web/GiselX.Web.csproj --no-restore -p:UseAppHost=false
```

Expected: controller tests pass and the web project builds with no errors.

- [ ] **Step 7: Commit Task 3**

```powershell
git add GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Index.cshtml GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml GiselX.Web/Models/DistributorTemplateMappingEditViewModel.cs GiselX.Web/Controllers/DistributorTemplateMappingsController.cs GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Edit.cshtml GiselX.Tests/DistributorTemplateMappingControllerTests.cs
git commit -m "feat: add distributor mapping management page"
```

---

### Task 4: Full Verification and Review

**Files:**
- Verify: all changed files from Tasks 1-3

**Interfaces:**
- Consumes: complete mapping-management feature.
- Produces: verified implementation ready for user testing.

- [ ] **Step 1: Run the complete automated suite**

Because the local web server may lock the normal output, use an isolated output directory when necessary:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --no-restore -p:UseAppHost=false -p:OutDir='D:\D\Fauzi\Geisa code\Git\GiselX\.codex-test-output\'
```

Expected: zero failed tests.

- [ ] **Step 2: Review the final diff against the specification**

Verify protected delete, company scoping, POST/anti-forgery actions, list fields, empty state, existing assignment display, and status/error messages.

- [ ] **Step 3: Request independent code review**

Use `superpowers:requesting-code-review`. Fix all Critical and Important findings and rerun the full suite after any change.

- [ ] **Step 4: Remove isolated test output**

Resolve and verify the exact `.codex-test-output` path is inside the workspace, then remove only that temporary directory.

- [ ] **Step 5: Commit verification fixes if any**

```powershell
git add GiselX.Repository.Interface GiselX.Repository GiselX.Service.Interface GiselX.Service GiselX.Web GiselX.Tests
git commit -m "test: verify distributor mapping management"
```
