# Sales Transaction Mapping Activation Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Sales Transaction mapping activation and deactivation deterministic by matching the working Stock mapping command pattern.

**Architecture:** Replace the browser-supplied activation boolean with separate server-owned `Activate` and `Deactivate` POST actions. Keep the existing service and repository behavior unchanged because ownership validation, audit updates, and transaction handling are already covered there.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC/Razor, xUnit.

## Global Constraints

- Keep the existing company-edit authorization and anti-forgery validation.
- Forward mapping ID, company ID, and authenticated username unchanged.
- `Activate` always sends `true`; `Deactivate` always sends `false`.
- Remove the MVC action boundary and form field that accept `isActive` from the browser.
- Activating one mapping must not deactivate other mappings.
- Preserve all unrelated working-tree changes.

---

### Task 1: Replace the Sales Transaction mapping toggle boundary

**Files:**
- Modify: `GiselX.Tests/DistributorTemplateMappingControllerTests.cs`
- Modify: `GiselX.Web/Controllers/DistributorTemplateMappingsController.cs`
- Modify: `GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Index.cshtml`

**Interfaces:**
- Produces: `POST Activate(int id, int companyId, CancellationToken cancellationToken)`.
- Produces: `POST Deactivate(int id, int companyId, CancellationToken cancellationToken)`.
- Consumes: `IDistributorTemplateMappingService.SetActiveAsync(int id, int companyId, bool isActive, string username, CancellationToken cancellationToken)`.
- Removes: public MVC action `SetActive(..., bool isActive, ...)` and the Sales Transaction mapping form field named `isActive`.

- [ ] **Step 1: Replace the old direct-action test with failing explicit-command tests**

Replace `SetActive_ForwardsCompanyStateAndAuthenticatedUsername` with:

```csharp
[Fact]
public async Task Activate_ForwardsTrueCompanyMappingAndAuthenticatedUsername()
{
    var service = new CapturingMappingService();
    var controller = CreateController(service);

    var result = await controller.Activate(7, 2, CancellationToken.None);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(DistributorTemplateMappingsController.Index), redirect.ActionName);
    Assert.Equal(2, redirect.RouteValues!["companyId"]);
    Assert.Equal((7, 2, true, "admin"), service.ActiveChange);
    Assert.Equal("Mapping activated.", controller.TempData["StatusMessage"]);
}

[Fact]
public async Task Deactivate_ForwardsFalseCompanyMappingAndAuthenticatedUsername()
{
    var service = new CapturingMappingService();
    var controller = CreateController(service);

    var result = await controller.Deactivate(7, 2, CancellationToken.None);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(DistributorTemplateMappingsController.Index), redirect.ActionName);
    Assert.Equal(2, redirect.RouteValues!["companyId"]);
    Assert.Equal((7, 2, false, "admin"), service.ActiveChange);
    Assert.Equal("Mapping deactivated.", controller.TempData["StatusMessage"]);
}
```

The production change that makes these tests pass is adding the two explicit controller actions with the correct server-owned constants.

- [ ] **Step 2: Run the controller tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~DistributorTemplateMappingControllerTests -p:BaseOutputPath=.tmp\sales-mapping-activation-red\bin\
```

Expected: compilation fails because `DistributorTemplateMappingsController.Activate` and `Deactivate` do not exist.

- [ ] **Step 3: Replace the boolean controller action with explicit commands**

Replace `SetActive` with:

```csharp
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Activate(int id, int companyId, CancellationToken cancellationToken) =>
    RedirectToList(companyId, await mappingService.SetActiveAsync(id, companyId, true,
        User.Identity?.Name ?? "unknown", cancellationToken));

[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Deactivate(int id, int companyId, CancellationToken cancellationToken) =>
    RedirectToList(companyId, await mappingService.SetActiveAsync(id, companyId, false,
        User.Identity?.Name ?? "unknown", cancellationToken));
```

Do not retain a public `SetActive` action that accepts `bool isActive`.

- [ ] **Step 4: Render only the applicable command form**

Replace the `SetActive` form in `Index.cshtml` with:

```cshtml
@if (mapping.IsActive)
{
    <form asp-action="Deactivate" method="post" class="d-inline">
        <input type="hidden" name="id" value="@mapping.Id" />
        <input type="hidden" name="companyId" value="@Model.CompanyId" />
        <button type="submit" class="btn btn-sm btn-outline-secondary">Deactivate</button>
    </form>
}
else
{
    <form asp-action="Activate" method="post" class="d-inline">
        <input type="hidden" name="id" value="@mapping.Id" />
        <input type="hidden" name="companyId" value="@Model.CompanyId" />
        <button type="submit" class="btn btn-sm btn-outline-secondary">Activate</button>
    </form>
}
```

Neither form may contain an input named `isActive`.

- [ ] **Step 5: Run focused tests and compile Razor to verify GREEN**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~DistributorTemplateMappingControllerTests|FullyQualifiedName~DistributorTemplateMappingServiceTests" -p:BaseOutputPath=.tmp\sales-mapping-activation-green\bin\
dotnet build GiselX.Web\GiselX.Web.csproj --no-restore -p:BaseOutputPath=.tmp\sales-mapping-activation-web\bin\
```

Expected: controller and service tests pass, and the Razor view compiles.

- [ ] **Step 6: Verify no client activation boolean remains**

Run:

```powershell
rg -n 'IActionResult> SetActive\(|name="isActive"' GiselX.Web\Controllers\DistributorTemplateMappingsController.cs GiselX.Web\Areas\Admin\Views\DistributorTemplateMappings\Index.cshtml
```

Expected: no public `SetActive` action and no `isActive` form field matches. The controller's internal call to the existing service method `SetActiveAsync` remains valid.

- [ ] **Step 7: Run the complete regression suite**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\sales-mapping-activation-final\bin\ --verbosity minimal
```

Expected: zero failed tests. Existing NuGet vulnerability, Mapperly, nullability, and xUnit analyzer warnings may remain; no new warning category is acceptable.

- [ ] **Step 8: Review workspace integrity**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and no unrelated files changed by this repair. Do not stage or commit pre-existing shared-worktree changes.
