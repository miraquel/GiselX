# Dynamic Company-Scoped Power BI Report Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let administrators configure public Power BI reports that appear as standalone, company-scoped sidebar pages for distributor users without redeploying GiselX.

**Architecture:** Persist one `PowerBiReport` row per report/company assignment, expose its CRUD operations through the existing Dapper repository and service layers, and resolve normal-user company scope from ASP.NET Core Identity on the server. Render active rows through a focused sidebar view component and a tenant-checked MVC viewer; keep all management endpoints in an `Administrator`-only Admin controller.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC and Identity, Entity Framework Core 10 migrations, SQL Server, Dapper, Razor, Bootstrap/Materio, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-21-dynamic-power-bi-report-menu-design.md`

## Global Constraints

- Each report configuration belongs to exactly one company.
- Each active configuration is a standalone sidebar item; do not add a catalog or Power BI parent menu.
- The configured `Name` is both the sidebar label and viewer heading.
- Company/distributor users may only list and view active reports assigned to their authenticated user's company.
- Create, edit, preview across companies, activate/deactivate, and delete operations require the `Administrator` role.
- Accept URL input only; never accept or render administrator-provided iframe HTML.
- Accept only HTTPS `app.powerbi.com/view` Publish-to-web URLs with a non-empty `r` query parameter.
- Publish to web is public data access, not a GiselX security boundary; show the approved warning on management forms.
- Use UTC for `CreatedDate` and `ModifiedDate`.
- Preserve all unrelated modified and untracked files already present in the worktree; stage only files named by the active task.

## File Structure

### Domain and persistence

- Create `GiselX.Domain/PowerBiReport.cs`: report entity and company relationship.
- Modify `GiselX.Domain/Company.cs`: `PowerBiReports` navigation collection.
- Modify `GiselX.Web/Context/GiselXDbContext.cs`: `DbSet` and fluent configuration.
- Create `GiselX.Web/Context/Migrations/20260821090000_AddPowerBiReports.cs`: schema migration.
- Create `GiselX.Web/Context/Migrations/20260821090000_AddPowerBiReports.Designer.cs`: generated migration metadata.
- Modify `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`: generated current model.
- Create `GiselX.Web/DatabaseScripts/20260821090000_AddPowerBiReports.sql`: guarded manual SQL deployment path.

### Repository and service

- Create `GiselX.Repository.Interface/IPowerBiReportRepository.cs`: persistence boundary.
- Create `GiselX.Repository/PowerBiReportRepository.cs`: parameterized SQL and company-scoped reads.
- Modify `GiselX.Repository/ServiceCollectionExtensions.cs`: repository registration.
- Modify `GiselX.Repository.Interface/ICompanyRepository.cs`: small ordered company lookup used by Admin forms.
- Modify `GiselX.Repository/CompanyRepository.cs`: implement the company lookup.
- Create `GiselX.Service.Dto/PowerBiReportDto.cs`: admin, save, menu, viewer, and company-option contracts.
- Create `GiselX.Service.Interface/IPowerBiReportService.cs`: application boundary.
- Create `GiselX.Service/PowerBiPublishUrlValidator.cs`: pure public-link validation and normalization.
- Create `GiselX.Service/PowerBiReportService.cs`: validation, audit, current-company scope, and transactions.
- Modify `GiselX.Service/ServiceCollectionExtensions.cs`: service registration.

### Web UI

- Create `GiselX.Web/Models/PowerBiReportAdminViewModels.cs`: Admin list/edit/delete models.
- Create `GiselX.Web/Controllers/PowerBiReportsController.cs`: authenticated company-scoped viewer.
- Create `GiselX.Web/Controllers/PowerBiReportMenuViewComponent.cs`: dynamic menu adapter.
- Create `GiselX.Web/Controllers/PowerBiReportsAdminController.cs`: Administrator-only management.
- Create `GiselX.Web/Views/PowerBiReports/View.cshtml`: responsive embedded viewer.
- Create `GiselX.Web/Views/Shared/Components/PowerBiReportMenu/Default.cshtml`: standalone dynamic menu items.
- Create `GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin/Index.cshtml`: management table and company filter.
- Create `GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin/Edit.cshtml`: create/edit form and public-data warning.
- Create `GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin/Delete.cshtml`: explicit confirmation.
- Modify `GiselX.Web/Views/Shared/Sections/Menu/_VerticalMenu.cshtml`: invoke dynamic items and add the admin link.
- Create `GiselX.Web/wwwroot/css/power-bi-reports.css`: viewer sizing/loading styles.
- Create `GiselX.Web/wwwroot/js/power-bi-reports.js`: loading and Fullscreen API behavior.

### Tests

- Create `GiselX.Tests/PowerBiPublishUrlValidatorTests.cs`.
- Create `GiselX.Tests/PowerBiReportPersistenceModelTests.cs`.
- Create `GiselX.Tests/PowerBiReportMigrationTests.cs`.
- Create `GiselX.Tests/PowerBiReportManualSqlTests.cs`.
- Create `GiselX.Tests/PowerBiReportRepositoryContractTests.cs`.
- Create `GiselX.Tests/PowerBiReportServiceTests.cs`.
- Create `GiselX.Tests/PowerBiReportsAdminControllerTests.cs`.
- Create `GiselX.Tests/PowerBiReportsControllerTests.cs`.
- Create `GiselX.Tests/PowerBiReportMenuViewComponentTests.cs`.
- Modify `GiselX.Tests/CompanyItemMappingEnablementTests.cs`, `GiselX.Tests/ItemMappingResolverTests.cs`, and `GiselX.Tests/CompaniesControllerTests.cs` only if the added ordered company lookup changes their fake interface implementations.

---

### Task 1: Validate and normalize Publish-to-web URLs

**Files:**
- Create: `GiselX.Service/PowerBiPublishUrlValidator.cs`
- Create: `GiselX.Tests/PowerBiPublishUrlValidatorTests.cs`

**Interfaces:**
- Produces: `PowerBiPublishUrlValidator.TryNormalize(string? value, out string normalizedUrl, out string error): bool`.
- Consumes: only `System.Uri`; it has no database, HTTP, or Identity dependency.

- [ ] **Step 1: Write the failing URL-validation tests**

Cover the accepted format and every rejected security boundary with explicit values:

```csharp
using GiselX.Service;

namespace GiselX.Tests;

public sealed class PowerBiPublishUrlValidatorTests
{
    [Theory]
    [InlineData("https://app.powerbi.com/view?r=abc123")]
    [InlineData("  https://APP.POWERBI.COM/view/?r=abc123&pageName=ReportSection  ")]
    public void TryNormalize_accepts_publish_to_web_urls(string value)
    {
        var valid = PowerBiPublishUrlValidator.TryNormalize(value, out var normalized, out var error);

        Assert.True(valid, error);
        Assert.StartsWith("https://app.powerbi.com/view", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(' ', normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<iframe src=\"https://app.powerbi.com/view?r=abc\"></iframe>")]
    [InlineData("http://app.powerbi.com/view?r=abc")]
    [InlineData("https://app.powerbi.com.evil.example/view?r=abc")]
    [InlineData("https://user@app.powerbi.com/view?r=abc")]
    [InlineData("https://app.powerbi.com/groups/1/reports/2")]
    [InlineData("https://app.powerbi.com/view")]
    [InlineData("https://app.powerbi.com/view?r=")]
    [InlineData("https://app.powerbi.com/view?r=abc#fragment")]
    public void TryNormalize_rejects_non_public_or_unsafe_values(string? value)
    {
        Assert.False(PowerBiPublishUrlValidator.TryNormalize(value, out var normalized, out var error));
        Assert.Empty(normalized);
        Assert.NotEmpty(error);
    }
}
```

- [ ] **Step 2: Run the validator tests and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiPublishUrlValidatorTests
```

Expected: FAIL because `PowerBiPublishUrlValidator` does not exist.

- [ ] **Step 3: Add the minimal pure validator**

Implement an ordinal, exact-host validator. Parse the query without introducing a new package:

```csharp
namespace GiselX.Service;

public static class PowerBiPublishUrlValidator
{
    public static bool TryNormalize(string? value, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate) || candidate.Any(char.IsControl))
            return Fail("A Power BI Publish-to-web URL is required.", out error);
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return Fail("The report URL must be an absolute HTTPS URL.", out error);
        if (!string.Equals(uri.Host, "app.powerbi.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            return Fail("The report URL must use app.powerbi.com.", out error);
        if (!string.Equals(uri.AbsolutePath.TrimEnd('/'), "/view", StringComparison.OrdinalIgnoreCase))
            return Fail("Use a Power BI Publish-to-web viewer URL.", out error);
        if (!string.IsNullOrEmpty(uri.Fragment) || !HasReportToken(uri.Query))
            return Fail("The Publish-to-web URL must contain a non-empty r parameter and no fragment.", out error);

        normalizedUrl = uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
            UriFormat.UriEscaped);
        return true;
    }

    private static bool HasReportToken(string query) => query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Any(pair => pair.Length == 2 &&
            string.Equals(Uri.UnescapeDataString(pair[0]), "r", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(Uri.UnescapeDataString(pair[1])));

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
```

- [ ] **Step 4: Run the validator tests**

Run the command from Step 2.

Expected: PASS for all accepted/rejected cases.

- [ ] **Step 5: Commit the validator slice**

```powershell
git add GiselX.Service/PowerBiPublishUrlValidator.cs GiselX.Tests/PowerBiPublishUrlValidatorTests.cs
git commit -m "feat: validate public Power BI report URLs"
```

### Task 2: Add the report persistence model and deployment artifacts

**Files:**
- Create: `GiselX.Domain/PowerBiReport.cs`
- Modify: `GiselX.Domain/Company.cs:32`
- Modify: `GiselX.Web/Context/GiselXDbContext.cs:14-44,99-120,430`
- Create: `GiselX.Web/Context/Migrations/20260821090000_AddPowerBiReports.cs`
- Create: `GiselX.Web/Context/Migrations/20260821090000_AddPowerBiReports.Designer.cs`
- Modify: `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`
- Create: `GiselX.Web/DatabaseScripts/20260821090000_AddPowerBiReports.sql`
- Create: `GiselX.Tests/PowerBiReportPersistenceModelTests.cs`
- Create: `GiselX.Tests/PowerBiReportMigrationTests.cs`
- Create: `GiselX.Tests/PowerBiReportManualSqlTests.cs`

**Interfaces:**
- Produces: `PowerBiReport` with `Id`, `CompanyId`, `Name`, `EmbedUrl`, `DisplayOrder`, `IsActive`, audit fields, and `Company`.
- Produces: SQL table `dbo.PowerBiReport` and indexes `UX_PowerBiReport_CompanyId_Name` and `IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name`.
- Consumes: existing `Company` entity and `GiselXDbContext`.

- [ ] **Step 1: Write the failing EF model contract test**

```csharp
using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public sealed class PowerBiReportPersistenceModelTests
{
    [Fact]
    public void Model_configures_company_scope_names_url_order_and_audit_contract()
    {
        using var db = new GiselXDbContext(new DbContextOptionsBuilder<GiselXDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXPowerBiModelTest;Trusted_Connection=True")
            .Options);
        var entity = db.Model.FindEntityType(typeof(PowerBiReport));

        Assert.NotNull(entity);
        Assert.Equal(150, entity!.FindProperty(nameof(PowerBiReport.Name))!.GetMaxLength());
        Assert.Equal(2048, entity.FindProperty(nameof(PowerBiReport.EmbedUrl))!.GetMaxLength());
        Assert.Equal(256, entity.FindProperty(nameof(PowerBiReport.CreatedBy))!.GetMaxLength());
        Assert.Equal(true, entity.FindProperty(nameof(PowerBiReport.IsActive))!.GetDefaultValue());
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PowerBiReport.CompanyId), nameof(PowerBiReport.Name)]));
    }
}
```

- [ ] **Step 2: Run the persistence test and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportPersistenceModelTests
```

Expected: FAIL because `PowerBiReport` is undefined.

- [ ] **Step 3: Add the entity, company navigation, and fluent model**

Create the entity:

```csharp
namespace GiselX.Domain;

public sealed class PowerBiReport
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmbedUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public Company Company { get; set; } = null!;
}
```

Add `DbSet<PowerBiReport> PowerBiReport`, `Company.PowerBiReports`, and this fluent configuration:

```csharp
modelBuilder.Entity<PowerBiReport>(entity =>
{
    entity.Property(value => value.Name).HasMaxLength(150).IsRequired();
    entity.Property(value => value.EmbedUrl).HasMaxLength(2048).IsRequired();
    entity.Property(value => value.IsActive).HasDefaultValue(true);
    entity.Property(value => value.CreatedDate).HasColumnType("datetime2");
    entity.Property(value => value.CreatedBy).HasMaxLength(256).IsRequired();
    entity.Property(value => value.ModifiedDate).HasColumnType("datetime2");
    entity.Property(value => value.ModifiedBy).HasMaxLength(256);
    entity.HasIndex(value => new { value.CompanyId, value.Name })
        .IsUnique().HasDatabaseName("UX_PowerBiReport_CompanyId_Name");
    entity.HasIndex(value => new { value.CompanyId, value.IsActive, value.DisplayOrder, value.Name })
        .HasDatabaseName("IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name");
    entity.HasOne(value => value.Company).WithMany(company => company.PowerBiReports)
        .HasForeignKey(value => value.CompanyId).OnDelete(DeleteBehavior.Restrict);
});
```

- [ ] **Step 4: Run the persistence test**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 5: Generate the migration**

Run:

```powershell
dotnet ef migrations add AddPowerBiReports --project GiselX.Web --startup-project GiselX.Web --output-dir Context/Migrations
```

Expected: a migration timestamped `20260821090000` in this plan's naming convention, plus updated designer and snapshot. If EF chooses a different timestamp, use its generated timestamp consistently in the migration, SQL history entry, tests, and filenames.

- [ ] **Step 6: Write migration and manual-script contract tests**

The migration tests inspect generated operations/source for the exact table, indexes, foreign-key restriction, defaults, and a clean `Down`. The manual SQL tests read the deployment file and assert rerun guards, transaction safety, schema verification, and migration history:

```csharp
Assert.Contains("CreateTable", migrationSource, StringComparison.Ordinal);
Assert.Contains("PowerBiReport", migrationSource, StringComparison.Ordinal);
Assert.Contains("UX_PowerBiReport_CompanyId_Name", migrationSource, StringComparison.Ordinal);
Assert.Contains("IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name", migrationSource, StringComparison.Ordinal);
Assert.Contains("onDelete: ReferentialAction.Restrict", migrationSource, StringComparison.Ordinal);

Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
Assert.Contains("OBJECT_ID(N'dbo.PowerBiReport'", sql, StringComparison.OrdinalIgnoreCase);
Assert.Contains("UX_PowerBiReport_CompanyId_Name", sql, StringComparison.Ordinal);
Assert.Contains("20260821090000_AddPowerBiReports", sql, StringComparison.Ordinal);
Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 7: Create the guarded manual SQL deployment script**

Follow the repository's existing SQL Server deployment contract: `SET XACT_ABORT ON`, `TRY/CATCH`, transaction, application lock, `OBJECT_ID` rerun guard, table and named indexes, FK to `dbo.Company(Id)` with no cascade, post-create schema checks that `THROW` on mismatch, and a guarded `__EFMigrationsHistory` insert using product version `10.0.8`.

- [ ] **Step 8: Run all persistence and deployment tests**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~PowerBiReportPersistenceModelTests|FullyQualifiedName~PowerBiReportMigrationTests|FullyQualifiedName~PowerBiReportManualSqlTests"
```

Expected: PASS.

- [ ] **Step 9: Commit the persistence slice**

```powershell
git add -p -- GiselX.Domain/Company.cs GiselX.Web/Context/GiselXDbContext.cs GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs
git add -- GiselX.Domain/PowerBiReport.cs GiselX.Web/Context/Migrations/20260821090000_AddPowerBiReports.cs GiselX.Web/Context/Migrations/20260821090000_AddPowerBiReports.Designer.cs GiselX.Web/DatabaseScripts/20260821090000_AddPowerBiReports.sql GiselX.Tests/PowerBiReportPersistenceModelTests.cs GiselX.Tests/PowerBiReportMigrationTests.cs GiselX.Tests/PowerBiReportManualSqlTests.cs
git commit -m "feat: persist company Power BI reports"
```

### Task 3: Add company-scoped repository operations

**Files:**
- Create: `GiselX.Repository.Interface/IPowerBiReportRepository.cs`
- Create: `GiselX.Repository/PowerBiReportRepository.cs`
- Modify: `GiselX.Repository/ServiceCollectionExtensions.cs:35-41`
- Modify: `GiselX.Repository.Interface/ICompanyRepository.cs`
- Modify: `GiselX.Repository/CompanyRepository.cs`
- Create: `GiselX.Tests/PowerBiReportRepositoryContractTests.cs`
- Modify: `GiselX.Tests/CompanyItemMappingEnablementTests.cs`
- Modify: `GiselX.Tests/ItemMappingResolverTests.cs`

**Interfaces:**
- Produces: `IPowerBiReportRepository.ListAdminAsync(int? companyId, CancellationToken)`.
- Produces: `ListActiveByCompanyAsync(int companyId, CancellationToken)`.
- Produces: `GetAdminByIdAsync(int id, CancellationToken)` and `GetActiveByIdAndCompanyAsync(int id, int companyId, CancellationToken)`.
- Produces: `NameExistsAsync`, `GetNextDisplayOrderAsync`, `InsertAsync`, `UpdateAsync`, `SetActiveAsync`, and `DeleteAsync`.
- Produces: `ICompanyRepository.GetAllAsync(CancellationToken)` for ordered Admin select options.
- Consumes: `PowerBiReport` and the existing scoped Dapper `IDbConnection`/`IDbTransaction`.

- [ ] **Step 1: Write failing repository SQL contract tests**

Read `PowerBiReportRepository.cs` from the solution root and require exact tenant/security predicates and parameterization:

```csharp
[Fact]
public void Normal_viewer_query_requires_id_company_and_active_state()
{
    var source = ReadRepository();
    Assert.Contains("Id = @Id", source, StringComparison.Ordinal);
    Assert.Contains("CompanyId = @CompanyId", source, StringComparison.Ordinal);
    Assert.Contains("IsActive = 1", source, StringComparison.Ordinal);
    Assert.DoesNotContain("$\"", source, StringComparison.Ordinal);
}

[Fact]
public void Menu_query_orders_by_display_order_then_name()
{
    var source = ReadRepository();
    Assert.Contains("ORDER BY DisplayOrder, Name", source, StringComparison.OrdinalIgnoreCase);
}
```

Add source checks for the company filter on Admin list, duplicate-name exclusion via `@ExcludeId`, parameterized mutations, and delete by exact `Id`.

- [ ] **Step 2: Run the repository contract tests and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportRepositoryContractTests
```

Expected: FAIL because the repository file does not exist.

- [ ] **Step 3: Define the repository interface**

```csharp
using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IPowerBiReportRepository
{
    Task<IReadOnlyList<PowerBiReport>> ListAdminAsync(int? companyId, CancellationToken token);
    Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token);
    Task<PowerBiReport?> GetAdminByIdAsync(int id, CancellationToken token);
    Task<PowerBiReport?> GetActiveByIdAndCompanyAsync(int id, int companyId, CancellationToken token);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId, CancellationToken token);
    Task<int> GetNextDisplayOrderAsync(int companyId, CancellationToken token);
    Task<int> InsertAsync(PowerBiReport report, CancellationToken token);
    Task<bool> UpdateAsync(PowerBiReport report, CancellationToken token);
    Task<bool> SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken token);
    Task<bool> DeleteAsync(int id, CancellationToken token);
}
```

- [ ] **Step 4: Implement parameterized Dapper SQL**

Use explicit columns. Normal-user reads must be exactly scoped:

```csharp
const string sql = """
    SELECT Id, CompanyId, Name, EmbedUrl, DisplayOrder, IsActive,
           CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
    FROM PowerBiReport
    WHERE Id = @Id AND CompanyId = @CompanyId AND IsActive = 1;
    """;
```

The menu list uses `WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY DisplayOrder, Name`. Admin list uses `WHERE (@CompanyId IS NULL OR CompanyId = @CompanyId) ORDER BY CompanyId, DisplayOrder, Name`. Return `false` when update/state/delete affects zero rows so the service can return `404`.

- [ ] **Step 5: Add the ordered company lookup and update existing fakes**

Add:

```csharp
Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken);
```

Implement with `SELECT Id, Name FROM Company ORDER BY Name;`. Update the two test fakes implementing `ICompanyRepository` to return an empty list so the solution compiles.

- [ ] **Step 6: Register the new repository**

Add:

```csharp
services.AddScoped<IPowerBiReportRepository, PowerBiReportRepository>();
```

- [ ] **Step 7: Run repository tests and compile dependent tests**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "FullyQualifiedName~PowerBiReportRepositoryContractTests|FullyQualifiedName~CompanyItemMappingEnablementTests|FullyQualifiedName~ItemMappingResolverTests"
```

Expected: PASS.

- [ ] **Step 8: Commit the repository slice**

```powershell
git add GiselX.Repository.Interface/IPowerBiReportRepository.cs GiselX.Repository/PowerBiReportRepository.cs GiselX.Repository/ServiceCollectionExtensions.cs GiselX.Repository.Interface/ICompanyRepository.cs GiselX.Repository/CompanyRepository.cs GiselX.Tests/PowerBiReportRepositoryContractTests.cs GiselX.Tests/CompanyItemMappingEnablementTests.cs GiselX.Tests/ItemMappingResolverTests.cs
git commit -m "feat: add company-scoped Power BI repository"
```

### Task 4: Add service contracts, validation, audit, and current-company scope

**Files:**
- Create: `GiselX.Service.Dto/PowerBiReportDto.cs`
- Create: `GiselX.Service.Interface/IPowerBiReportService.cs`
- Create: `GiselX.Service/PowerBiReportService.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs:14-31`
- Create: `GiselX.Tests/PowerBiReportServiceTests.cs`

**Interfaces:**
- Produces: `PowerBiReportDto`, `SavePowerBiReportDto`, `PowerBiReportMenuItemDto`, `PowerBiReportViewerDto`, and `CompanyOptionDto`.
- Produces: current-company `ListMenuAsync` and `GetViewerAsync` methods that accept no caller-supplied company ID.
- Produces: Admin list/get/save/state/delete methods.
- Consumes: `IPowerBiReportRepository`, `ICompanyRepository`, `IDbTransaction`, `UserClaimDto`, `UserManager<AppIdentityUser>`, and `PowerBiPublishUrlValidator`.

- [ ] **Step 1: Write failing service tests with capturing fakes**

Cover these named behaviors:

```csharp
[Fact]
public async Task ListMenuAsync_uses_authenticated_users_company_and_returns_active_ordered_items()
{
    var repository = new FakePowerBiReportRepository
    {
        Active = [new PowerBiReport { Id = 3, CompanyId = 7, Name = "Sales", DisplayOrder = 2 }]
    };
    var service = CreateService(repository, userCompanyId: 7);

    var response = await service.ListMenuAsync(default);

    Assert.Equal(7, repository.ListedCompanyId);
    Assert.Equal("Sales", Assert.Single(response.Data!).Name);
}

[Fact]
public async Task GetViewerAsync_cannot_forward_a_company_id_from_the_caller()
{
    var repository = new FakePowerBiReportRepository();
    var service = CreateService(repository, userCompanyId: 7);

    await service.GetViewerAsync(42, default);

    Assert.Equal((42, 7), repository.ViewerScope);
}

[Fact]
public async Task SaveAsync_rejects_invalid_url_without_committing()
{
    var repository = new FakePowerBiReportRepository();
    var transaction = new FakeTransaction();
    var service = CreateService(repository, transaction, userCompanyId: 7);

    var response = await service.SaveAsync(new SavePowerBiReportDto
        { CompanyId = 7, Name = "Sales", EmbedUrl = "https://evil.example/report", DisplayOrder = 10 },
        "admin", default);

    Assert.Equal(400, response.StatusCode);
    Assert.Contains(response.Errors, error => error.Contains("app.powerbi.com", StringComparison.OrdinalIgnoreCase));
    Assert.False(transaction.CommitCalled);
}

[Fact]
public async Task SaveAsync_rejects_duplicate_company_name_without_committing()
{
    var repository = new FakePowerBiReportRepository { DuplicateName = true };
    var transaction = new FakeTransaction();
    var service = CreateService(repository, transaction, userCompanyId: 7);

    var response = await service.SaveAsync(new SavePowerBiReportDto
        { CompanyId = 7, Name = "Sales", EmbedUrl = "https://app.powerbi.com/view?r=abc", DisplayOrder = 10 },
        "admin", default);

    Assert.Equal(400, response.StatusCode);
    Assert.Contains(response.Errors, error => error.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    Assert.False(transaction.CommitCalled);
}

[Fact]
public async Task SaveAsync_normalizes_values_and_sets_utc_creation_audit()
{
    var repository = new FakePowerBiReportRepository();
    var transaction = new FakeTransaction();
    var service = CreateService(repository, transaction, userCompanyId: 7);
    var before = DateTime.UtcNow;

    var response = await service.SaveAsync(new SavePowerBiReportDto
        { CompanyId = 7, Name = "  Sales  ", EmbedUrl = " https://app.powerbi.com/view?r=abc ", DisplayOrder = 10 },
        "admin", default);

    Assert.True(response.IsSuccess);
    Assert.Equal("Sales", repository.Inserted!.Name);
    Assert.Equal("https://app.powerbi.com/view?r=abc", repository.Inserted.EmbedUrl);
    Assert.Equal("admin", repository.Inserted.CreatedBy);
    Assert.InRange(repository.Inserted.CreatedDate, before, DateTime.UtcNow);
    Assert.True(transaction.CommitCalled);
}

[Fact]
public async Task SaveAsync_update_preserves_created_audit_and_sets_modified_audit()
{
    var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    var repository = new FakePowerBiReportRepository
    {
        AdminById = new PowerBiReport
            { Id = 4, CompanyId = 7, Name = "Old", EmbedUrl = "https://app.powerbi.com/view?r=old", CreatedBy = "first", CreatedDate = created }
    };
    var service = CreateService(repository, new FakeTransaction(), userCompanyId: 7);

    var response = await service.SaveAsync(new SavePowerBiReportDto
        { Id = 4, CompanyId = 7, Name = "New", EmbedUrl = "https://app.powerbi.com/view?r=new", DisplayOrder = 20 },
        "admin", default);

    Assert.True(response.IsSuccess);
    Assert.Equal(created, repository.Updated!.CreatedDate);
    Assert.Equal("first", repository.Updated.CreatedBy);
    Assert.Equal("admin", repository.Updated.ModifiedBy);
    Assert.NotNull(repository.Updated.ModifiedDate);
}

[Fact]
public async Task SetActiveAsync_returns_not_found_when_repository_affects_no_row()
{
    var service = CreateService(new FakePowerBiReportRepository { StateChangeResult = false },
        new FakeTransaction(), userCompanyId: 7);

    var response = await service.SetActiveAsync(404, false, "admin", default);

    Assert.Equal(404, response.StatusCode);
    Assert.False(response.IsSuccess);
}
```

Use the existing `SingleUserStore` pattern from `AdministratorServiceScopeTests` to create a `UserManager<AppIdentityUser>` whose user has `CompanyId = 7`. Implement every fake repository method explicitly; unsupported paths throw `NotSupportedException`.

- [ ] **Step 2: Run the service tests and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportServiceTests
```

Expected: FAIL because the DTOs, interface, and service do not exist.

- [ ] **Step 3: Add focused DTO records/classes**

```csharp
namespace GiselX.Service.Dto;

public sealed class PowerBiReportDto
{
    public int Id { get; init; }
    public int CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? ModifiedDate { get; init; }
    public string? ModifiedBy { get; init; }
}

public sealed class SavePowerBiReportDto
{
    public int? Id { get; init; }
    public int CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record PowerBiReportMenuItemDto(int Id, string Name);
public sealed record PowerBiReportViewerDto(int Id, string Name, string EmbedUrl);
public sealed record CompanyOptionDto(int Id, string Name);
```

- [ ] **Step 4: Define the service interface**

```csharp
public interface IPowerBiReportService
{
    Task<ServiceResponse<IReadOnlyList<PowerBiReportDto>>> ListAdminAsync(int? companyId, CancellationToken token);
    Task<ServiceResponse<PowerBiReportDto?>> GetAdminAsync(int id, CancellationToken token);
    Task<ServiceResponse<IReadOnlyList<CompanyOptionDto>>> ListCompanyOptionsAsync(CancellationToken token);
    Task<ServiceResponse<int>> GetNextDisplayOrderAsync(int companyId, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(SavePowerBiReportDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, CancellationToken token);
    Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token);
    Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token);
}
```

- [ ] **Step 5: Implement validation and Admin operations**

Validate `CompanyId > 0`, a trimmed non-empty name of at most 150 characters, `DisplayOrder >= 0`, URL length at most 2,048, URL normalization, company existence via the option lookup, and duplicate name via `NameExistsAsync`. On create, set both audit creation fields. On edit, load by ID, apply company/name/URL/order/active values, preserve creation audit, and set modification audit.

Return `ServiceResponse` errors with HTTP 400 for validation, 404 for missing rows, and clear success messages. Commit exactly once after a successful mutation and never after a failed validation/lookup.

- [ ] **Step 6: Implement current-company menu and viewer operations**

Resolve scope internally:

```csharp
private async Task<int> CurrentCompanyIdAsync()
{
    var user = await userManager.FindByNameAsync(userClaimDto.Username)
        ?? throw new InvalidOperationException("User not found.");
    return user.CompanyId;
}
```

`ListMenuAsync` maps `ListActiveByCompanyAsync(await CurrentCompanyIdAsync())` to ID/name only. `GetViewerAsync` calls `GetActiveByIdAndCompanyAsync(id, await CurrentCompanyIdAsync())`; return 404 with no cross-company detail when null.

- [ ] **Step 7: Register the service and run its tests**

Add:

```csharp
services.AddScoped<IPowerBiReportService, PowerBiReportService>();
```

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 8: Commit the service slice**

```powershell
git add GiselX.Service.Dto/PowerBiReportDto.cs GiselX.Service.Interface/IPowerBiReportService.cs GiselX.Service/PowerBiReportService.cs GiselX.Service/ServiceCollectionExtensions.cs GiselX.Tests/PowerBiReportServiceTests.cs
git commit -m "feat: add Power BI report application service"
```

### Task 5: Build Administrator-only report management

**Files:**
- Create: `GiselX.Web/Models/PowerBiReportAdminViewModels.cs`
- Create: `GiselX.Web/Controllers/PowerBiReportsAdminController.cs`
- Create: `GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin/Index.cshtml`
- Create: `GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin/Edit.cshtml`
- Create: `GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin/Delete.cshtml`
- Create: `GiselX.Tests/PowerBiReportsAdminControllerTests.cs`

**Interfaces:**
- Produces: `/Admin/PowerBiReportsAdmin` management routes and `Administrator` role boundary.
- Produces: create/edit/delete/state-change forms with antiforgery protection.
- Consumes: `IPowerBiReportService` Admin methods and DTOs from Task 4.

- [ ] **Step 1: Write failing authorization and controller-flow tests**

Use reflection to ensure the role boundary exists at controller level, then fake the service for action behavior:

```csharp
[Fact]
public void Controller_requires_administrator_role()
{
    var attribute = Assert.Single(typeof(PowerBiReportsAdminController)
        .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
        .Cast<AuthorizeAttribute>());
    Assert.Equal("Administrator", attribute.Roles);
}

[Fact]
public async Task Save_invalid_model_redisplays_form_with_company_options()
{
    var service = new FakePowerBiReportService
    {
        CompanyOptions = [new CompanyOptionDto(7, "Distributor")]
    };
    var controller = new PowerBiReportsAdminController(service);
    controller.ModelState.AddModelError(nameof(PowerBiReportEditViewModel.Name), "Name is required.");
    var model = new PowerBiReportEditViewModel { CompanyId = 7 };

    var result = Assert.IsType<ViewResult>(await controller.Save(model, default));

    Assert.Equal("Edit", result.ViewName);
    Assert.Single(Assert.IsType<PowerBiReportEditViewModel>(result.Model).Companies);
    Assert.False(service.SaveCalled);
}

[Fact]
public async Task Save_service_error_adds_model_error_and_does_not_redirect()
{
    var service = new FakePowerBiReportService
    {
        CompanyOptions = [new CompanyOptionDto(7, "Distributor")],
        SaveResponse = new ServiceResponse<int>
            { StatusCode = 400, Errors = ["A report with this name already exists for the company."] }
    };
    var controller = new PowerBiReportsAdminController(service);
    var model = new PowerBiReportEditViewModel
        { CompanyId = 7, Name = "Sales", EmbedUrl = "https://app.powerbi.com/view?r=abc" };

    var result = Assert.IsType<ViewResult>(await controller.Save(model, default));

    Assert.Equal("Edit", result.ViewName);
    Assert.Contains(controller.ModelState[string.Empty]!.Errors,
        error => error.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase));
}

[Theory]
[InlineData(nameof(PowerBiReportsAdminController.Activate))]
[InlineData(nameof(PowerBiReportsAdminController.Deactivate))]
[InlineData(nameof(PowerBiReportsAdminController.DeleteConfirmed))]
public void State_changing_actions_are_post_only_and_validate_antiforgery(string methodName)
{
    var method = typeof(PowerBiReportsAdminController).GetMethod(methodName)!;
    Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
    Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
}

[Fact]
public async Task Preview_uses_admin_lookup_instead_of_current_company_lookup()
{
    var service = new FakePowerBiReportService
    {
        AdminReport = new PowerBiReportDto
            { Id = 9, CompanyId = 4, Name = "Other Company", EmbedUrl = "https://app.powerbi.com/view?r=abc" }
    };
    var controller = new PowerBiReportsAdminController(service);

    var result = Assert.IsType<ViewResult>(await controller.Preview(9, default));

    Assert.Equal("~/Views/PowerBiReports/View.cshtml", result.ViewName);
    Assert.Equal(9, service.AdminLookupId);
    Assert.False(service.ViewerLookupCalled);
}
```

- [ ] **Step 2: Run Admin controller tests and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportsAdminControllerTests
```

Expected: FAIL because the controller and models do not exist.

- [ ] **Step 3: Add view models with MVC validation**

```csharp
public sealed class PowerBiReportEditViewModel
{
    public int? Id { get; set; }
    [Range(1, int.MaxValue)] public int CompanyId { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(2048)] public string EmbedUrl { get; set; } = string.Empty;
    [Range(0, int.MaxValue)] public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<CompanyOptionDto> Companies { get; set; } = [];
}

public sealed record PowerBiReportAdminIndexViewModel(
    IReadOnlyList<PowerBiReportDto> Reports,
    IReadOnlyList<CompanyOptionDto> Companies,
    int? CompanyId);
```

Add a delete model containing ID, company name, report name, and the public URL for clear confirmation.

- [ ] **Step 4: Add the controller and security attributes**

```csharp
[Area("Admin")]
[Authorize(Roles = "Administrator")]
public sealed class PowerBiReportsAdminController(IPowerBiReportService service) : Controller
```

Add GET `Index`, GET `Create`, GET `Edit`, POST `Save`, POST `Activate`, POST `Deactivate`, GET/POST `Delete`, and GET `Preview`. Every POST has `[ValidateAntiForgeryToken]`; state-changing actions are POST-only. Load company options before returning any edit form, including failed submissions.

- [ ] **Step 5: Build the management views**

The index renders company filter, table, active badge, display order, and explicit buttons. `Edit.cshtml` contains the approved warning:

```html
<div class="alert alert-warning" role="alert">
  Publish to web is public. Anyone with this URL can view the report and may access underlying detail data. Add only data approved for public Internet access.
</div>
```

Use tag helpers for all values and URLs. The URL input is plain text/URL only. Delete names the report and company and posts an antiforgery token.

- [ ] **Step 6: Run Admin tests**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 7: Commit the Admin slice**

```powershell
git add GiselX.Web/Models/PowerBiReportAdminViewModels.cs GiselX.Web/Controllers/PowerBiReportsAdminController.cs GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin GiselX.Tests/PowerBiReportsAdminControllerTests.cs
git commit -m "feat: manage company Power BI reports"
```

### Task 6: Add the tenant-checked embedded viewer

**Files:**
- Create: `GiselX.Web/Controllers/PowerBiReportsController.cs`
- Create: `GiselX.Web/Views/PowerBiReports/View.cshtml`
- Create: `GiselX.Web/wwwroot/css/power-bi-reports.css`
- Create: `GiselX.Web/wwwroot/js/power-bi-reports.js`
- Create: `GiselX.Tests/PowerBiReportsControllerTests.cs`

**Interfaces:**
- Produces: authenticated `GET /PowerBiReports/View/{id:int}`.
- Consumes: `IPowerBiReportService.GetViewerAsync(int id, CancellationToken)`; no company ID is accepted by the action.

- [ ] **Step 1: Write failing viewer controller tests**

```csharp
[Fact]
public void Controller_requires_authenticated_user()
{
    Assert.Contains(typeof(PowerBiReportsController).GetCustomAttributes(true),
        value => value is AuthorizeAttribute attribute && string.IsNullOrEmpty(attribute.Roles));
}

[Fact]
public async Task View_returns_not_found_for_missing_inactive_or_other_company_report()
{
    var controller = new PowerBiReportsController(new ViewerService(data: null, statusCode: 404));
    Assert.IsType<NotFoundResult>(await controller.View(42, default));
}

[Fact]
public async Task View_renders_company_scoped_viewer_model()
{
    var dto = new PowerBiReportViewerDto(42, "Sales", "https://app.powerbi.com/view?r=abc");
    var result = Assert.IsType<ViewResult>(await new PowerBiReportsController(
        new ViewerService(dto, 200)).View(42, default));
    Assert.Same(dto, result.Model);
}
```

- [ ] **Step 2: Run viewer controller tests and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportsControllerTests
```

Expected: FAIL because the viewer controller does not exist.

- [ ] **Step 3: Implement the minimal viewer controller**

```csharp
[Authorize]
public sealed class PowerBiReportsController(IPowerBiReportService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> View(int id, CancellationToken token)
    {
        var response = await service.GetViewerAsync(id, token);
        return response.Data is null ? NotFound() : View(response.Data);
    }
}
```

- [ ] **Step 4: Build the encoded responsive viewer**

Use a strongly typed Razor model and a normal `src="@Model.EmbedUrl"` attribute so Razor encodes the value. Include the stylesheet and script through page sections. The frame container has a spinner, a permanent new-tab fallback with `target="_blank" rel="noopener noreferrer"`, and a Fullscreen API button.

The JavaScript only reacts to the iframe's outer `load` event; it does not attempt to read cross-origin Power BI content:

```javascript
document.addEventListener('DOMContentLoaded', () => {
  const frame = document.getElementById('power-bi-frame');
  const loading = document.getElementById('power-bi-loading');
  const container = document.getElementById('power-bi-container');
  frame?.addEventListener('load', () => loading?.classList.add('d-none'));
  document.getElementById('power-bi-fullscreen')?.addEventListener('click', async () => {
    if (container?.requestFullscreen) await container.requestFullscreen();
  });
});
```

CSS uses `min-height: 70vh`, an iframe width/height of `100%`, and a mobile-safe minimum height.

- [ ] **Step 5: Run viewer controller tests**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 6: Commit the viewer slice**

```powershell
git add GiselX.Web/Controllers/PowerBiReportsController.cs GiselX.Web/Views/PowerBiReports GiselX.Web/wwwroot/css/power-bi-reports.css GiselX.Web/wwwroot/js/power-bi-reports.js GiselX.Tests/PowerBiReportsControllerTests.cs
git commit -m "feat: view company Power BI reports"
```

### Task 7: Render active company reports as standalone sidebar items

**Files:**
- Create: `GiselX.Web/Controllers/PowerBiReportMenuViewComponent.cs`
- Create: `GiselX.Web/Views/Shared/Components/PowerBiReportMenu/Default.cshtml`
- Modify: `GiselX.Web/Views/Shared/Sections/Menu/_VerticalMenu.cshtml:24-73,81-112`
- Create: `GiselX.Tests/PowerBiReportMenuViewComponentTests.cs`

**Interfaces:**
- Produces: `PowerBiReportMenuViewComponent.InvokeAsync(): Task<IViewComponentResult>`.
- Consumes: `IPowerBiReportService.ListMenuAsync(CancellationToken)` and `PowerBiReportMenuItemDto`.

- [ ] **Step 1: Write failing menu-component tests**

```csharp
[Fact]
public async Task InvokeAsync_passes_only_service_menu_items_to_the_view()
{
    var items = new[]
    {
        new PowerBiReportMenuItemDto(2, "Stock Dashboard"),
        new PowerBiReportMenuItemDto(3, "Sales Dashboard")
    };
    var component = new PowerBiReportMenuViewComponent(new MenuService(items));

    var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
    Assert.Equal(items, Assert.IsAssignableFrom<IReadOnlyList<PowerBiReportMenuItemDto>>(result.ViewData!.Model));
}

[Fact]
public void Component_has_no_company_id_input()
{
    var method = typeof(PowerBiReportMenuViewComponent).GetMethod(nameof(PowerBiReportMenuViewComponent.InvokeAsync))!;
    Assert.Empty(method.GetParameters());
}
```

- [ ] **Step 2: Run menu-component tests and confirm the expected failure**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportMenuViewComponentTests
```

Expected: FAIL because the component does not exist.

- [ ] **Step 3: Implement the component**

```csharp
public sealed class PowerBiReportMenuViewComponent(IPowerBiReportService service) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true)
            return View(Array.Empty<PowerBiReportMenuItemDto>());
        var response = await service.ListMenuAsync(HttpContext.RequestAborted);
        return View(response.Data ?? []);
    }
}
```

- [ ] **Step 4: Render standalone encoded links and active state**

The component view loops directly with no wrapper menu:

```cshtml
@model IReadOnlyList<GiselX.Service.Dto.PowerBiReportMenuItemDto>
@{
    var currentId = ViewContext.RouteData.Values["controller"]?.ToString() == "PowerBiReports"
        && int.TryParse(ViewContext.RouteData.Values["id"]?.ToString(), out var id) ? id : (int?)null;
}
@foreach (var report in Model)
{
    <li class="menu-item@(currentId == report.Id ? " active" : "")">
        <a asp-area="" asp-controller="PowerBiReports" asp-action="View" asp-route-id="@report.Id" class="menu-link">
            <i class="menu-icon tf-icons ri-bar-chart-box-line"></i>
            <div>@report.Name</div>
        </a>
    </li>
}
```

Invoke it after the existing static operational items in Main Menu:

```cshtml
@await Component.InvokeAsync("PowerBiReportMenu")
```

Do not add a report header, parent link, or empty-state item.

- [ ] **Step 5: Add the Administrator management navigation link**

Within the existing Admin section, show **Manage Power BI Reports** only for `User.IsInRole("Administrator")`, linking to the Admin controller. Keep existing permission-based links unchanged.

- [ ] **Step 6: Run menu tests and a Razor build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter FullyQualifiedName~PowerBiReportMenuViewComponentTests
dotnet build GiselX.Web/GiselX.Web.csproj --no-restore
```

Expected: both commands PASS; Razor resolves the view component and tag helpers.

- [ ] **Step 7: Commit the navigation slice**

```powershell
git add GiselX.Web/Controllers/PowerBiReportMenuViewComponent.cs GiselX.Web/Views/Shared/Components/PowerBiReportMenu GiselX.Web/Views/Shared/Sections/Menu/_VerticalMenu.cshtml GiselX.Tests/PowerBiReportMenuViewComponentTests.cs
git commit -m "feat: render dynamic Power BI report menus"
```

### Task 8: Verify the integrated feature and document operational checks

**Files:**
- Modify only if verification exposes a defect: files created or listed in Tasks 1-7.
- Read: `docs/superpowers/specs/2026-08-21-dynamic-power-bi-report-menu-design.md`.

**Interfaces:**
- Consumes: every interface produced by Tasks 1-7.
- Produces: a buildable, tested feature matching every acceptance criterion in the spec.

- [ ] **Step 1: Run formatting and whitespace checks on feature files**

```powershell
dotnet format GiselX.sln --no-restore --include GiselX.Domain/PowerBiReport.cs GiselX.Repository/PowerBiReportRepository.cs GiselX.Service/PowerBiReportService.cs GiselX.Web/Controllers/PowerBiReportsController.cs GiselX.Web/Controllers/PowerBiReportsAdminController.cs GiselX.Web/Controllers/PowerBiReportMenuViewComponent.cs
git diff --check
```

Expected: commands exit successfully and report no whitespace errors in feature files. Do not mechanically format unrelated dirty files.

- [ ] **Step 2: Run the complete automated test suite**

```powershell
dotnet test GiselX.sln --no-restore
```

Expected: all tests PASS with zero failures.

- [ ] **Step 3: Build the complete solution**

```powershell
dotnet build GiselX.sln --no-restore
```

Expected: build succeeds with zero errors.

- [ ] **Step 4: Generate an idempotent migration script for review**

```powershell
dotnet ef migrations script 20260811180000_AddCompanyItemIdentityMapping 20260821090000_AddPowerBiReports --idempotent --project GiselX.Web --startup-project GiselX.Web --output tmp/PowerBiReports.ef.sql
```

Expected: the generated script creates only `PowerBiReport`, its indexes/FK, and its migration-history row. Compare its schema contract to `GiselX.Web/DatabaseScripts/20260821090000_AddPowerBiReports.sql`; do not commit the temporary file.

- [ ] **Step 5: Manually verify two-company isolation**

Run GiselX against a disposable development database, create these records through the Admin UI, and verify the exact results:

| Company | Name | Order | Active | Expected |
| --- | --- | ---: | --- | --- |
| Company A | Sales Dashboard | 20 | Yes | Company A sidebar and viewer only |
| Company A | Stock Dashboard | 10 | Yes | Appears before Sales Dashboard |
| Company A | Hidden Dashboard | 5 | No | No menu item; direct normal URL is 404 |
| Company B | Monthly Performance | 10 | Yes | Company B sidebar and viewer only |

Also change a Company A viewer ID to Company B's ID and confirm `404`; confirm non-admin POSTs to every management action are forbidden; confirm the Admin preview works; confirm rename/order/state changes appear on the next full page load; confirm full screen and new-tab fallback work at desktop and mobile widths.

- [ ] **Step 6: Re-read the spec acceptance criteria and audit coverage**

Check each acceptance criterion in the linked spec against a passing automated test or the manual matrix. Confirm there is no catalog, no parent report menu, no raw HTML input, no caller-provided company scope in normal routes, and no management action lacking the Administrator boundary.

- [ ] **Step 7: Commit only verification fixes, if any**

If verification required code changes, stage only those feature files and their regression tests:

Review the feature-only diff, then use interactive staging so pre-existing edits in shared files are not included:

```powershell
git add -p -- GiselX.Domain/Company.cs GiselX.Web/Context/GiselXDbContext.cs GiselX.Repository/ServiceCollectionExtensions.cs GiselX.Repository.Interface/ICompanyRepository.cs GiselX.Repository/CompanyRepository.cs GiselX.Service/ServiceCollectionExtensions.cs GiselX.Web/Views/Shared/Sections/Menu/_VerticalMenu.cshtml
git add -- GiselX.Domain/PowerBiReport.cs GiselX.Repository.Interface/IPowerBiReportRepository.cs GiselX.Repository/PowerBiReportRepository.cs GiselX.Service.Dto/PowerBiReportDto.cs GiselX.Service.Interface/IPowerBiReportService.cs GiselX.Service/PowerBiPublishUrlValidator.cs GiselX.Service/PowerBiReportService.cs GiselX.Web/Controllers/PowerBiReportsController.cs GiselX.Web/Controllers/PowerBiReportsAdminController.cs GiselX.Web/Controllers/PowerBiReportMenuViewComponent.cs GiselX.Web/Models/PowerBiReportAdminViewModels.cs GiselX.Web/Views/PowerBiReports GiselX.Web/Views/Shared/Components/PowerBiReportMenu GiselX.Web/Areas/Admin/Views/PowerBiReportsAdmin GiselX.Web/wwwroot/css/power-bi-reports.css GiselX.Web/wwwroot/js/power-bi-reports.js GiselX.Tests/PowerBiReport*.cs
git diff --cached --check
git commit -m "fix: complete Power BI report verification"
```

If no verification fix was required, do not create an empty commit.

## Completion Conditions

- All Task 1-8 checkboxes are complete.
- The complete test suite and solution build pass from fresh command output.
- The migration and manual SQL deployment contracts agree.
- The two-company manual matrix passes.
- Only feature files are staged or committed; pre-existing unrelated worktree changes remain untouched.
- The final handoff repeats the Publish-to-web public-data warning and names the migration/deployment step.
