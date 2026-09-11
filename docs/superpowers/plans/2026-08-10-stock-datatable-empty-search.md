# Stock DataTable Empty Search Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Stock DataTable API accept an empty global search without returning HTTP 400.

**Architecture:** The API request contract will explicitly allow a nullable search term, matching ASP.NET Core's empty-query-string model-binding behavior. The Stock DataTable client will omit `SearchTerm` unless the user entered a non-empty value, while the repository's existing `string.IsNullOrWhiteSpace` guard continues to interpret null as no search.

**Tech Stack:** ASP.NET Core MVC model validation, C# nullable reference types, Razor/JavaScript, xUnit.

## Global Constraints

- Do not change the database schema or data.
- Do not change Stock upload, mapping, validation, or publication behavior.
- Preserve non-empty global Stock searches.
- Accept empty search values from the Stock page and other API clients.

---

### Task 1: Accept empty Stock DataTable searches

**Files:**
- Create: `GiselX.Tests/PagedListRequestDtoValidationTests.cs`
- Modify: `GiselX.Service.Dto/Common/ListRequestDto.cs`
- Modify: `GiselX.Domain/Common/ListRequest.cs`
- Modify: `GiselX.Web/Views/Stocks/Index.cshtml`

**Interfaces:**
- Consumes: ASP.NET Core MVC validation metadata for `PagedListRequestDto`.
- Produces: nullable `ListRequestDto.SearchTerm` and `ListRequest.SearchTerm`; Stock AJAX requests that include `SearchTerm` only for non-empty searches.

- [ ] **Step 1: Write the failing MVC validation test**

Create `PagedListRequestDtoValidationTests.cs`:

```csharp
using GiselX.Service.Dto.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Tests;

public sealed class PagedListRequestDtoValidationTests
{
    [Fact]
    public void Null_search_term_is_valid_for_api_model_binding()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IObjectModelValidator>();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        validator.Validate(actionContext, null, string.Empty,
            new PagedListRequestDto { SearchTerm = null! });

        Assert.DoesNotContain(modelState, entry =>
            entry.Key == nameof(PagedListRequestDto.SearchTerm) && entry.Value!.Errors.Count > 0);
    }
}
```

- [ ] **Step 2: Run the targeted test and verify RED**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\stock-search-red\bin\ --filter FullyQualifiedName~PagedListRequestDtoValidationTests --verbosity minimal
```

Expected: the test fails because MVC treats the current non-nullable `SearchTerm` property as required.

- [ ] **Step 3: Make the request contract nullable**

Change both request models from:

```csharp
public string SearchTerm { get; set; } = string.Empty;
```

to:

```csharp
public string? SearchTerm { get; set; }
```

Files:

- `GiselX.Service.Dto/Common/ListRequestDto.cs`
- `GiselX.Domain/Common/ListRequest.cs`

- [ ] **Step 4: Omit an empty Stock search parameter**

In `GiselX.Web/Views/Stocks/Index.cshtml`, build the DataTables query without an unconditional `SearchTerm` property:

```javascript
const q = {
    pageNumber: (d.start / d.length) + 1,
    pageSize: d.length
};
if (d.search?.value) {
    q.SearchTerm = d.search.value;
}
```

Keep the existing sorting parameter behavior unchanged.

- [ ] **Step 5: Run the targeted test and verify GREEN**

Run the Step 2 command again.

Expected: one test passes with zero failures.

- [ ] **Step 6: Run the complete test suite**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\stock-search-final\bin\ --verbosity minimal
```

Expected: all tests pass. Existing unrelated NuGet and compiler warnings may remain.

- [ ] **Step 7: Manually verify the request shape**

Restart the application, open `/Stocks`, and confirm in Chrome Network tools that the initial `/api/Stocks/Select` request does not contain `SearchTerm=` and returns HTTP 200. Entering text in the DataTable search box must add a non-empty `SearchTerm` query parameter.

- [ ] **Step 8: Commit if Git is available**

```powershell
git add GiselX.Tests\PagedListRequestDtoValidationTests.cs GiselX.Service.Dto\Common\ListRequestDto.cs GiselX.Domain\Common\ListRequest.cs GiselX.Web\Views\Stocks\Index.cshtml docs\superpowers\specs\2026-08-10-stock-datatable-empty-search-design.md docs\superpowers\plans\2026-08-10-stock-datatable-empty-search.md
git commit -m "fix: accept empty stock table search"
```

If Git remains unavailable, leave the verified changes in the current checkout and report that no commit was created.
