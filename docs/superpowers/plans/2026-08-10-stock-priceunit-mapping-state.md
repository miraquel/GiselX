# Stock PriceUnit and Mapping State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Stock mapping activation deterministic and add an optional, non-negative `PriceUnit` value to mapping, upload, staging, publication, listing, and downloads.

**Architecture:** Replace the browser-supplied activation boolean with separate server-owned `Activate` and `Deactivate` POST commands. Append `PriceUnit` to the canonical enum, carry nullable `decimal(18,2)` values through the existing parser/staging/publication boundaries, and add the field to Stock reads and exports. Deploy the two nullable database columns through a compatibility-checking EF migration and matching manual SQL script.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC/Razor, EF Core 10 SQL Server migrations, Dapper/SqlBulkCopy, ClosedXML, JavaScript DataTables, xUnit.

## Global Constraints

- `PriceUnit` is optional and nullable throughout the Stock pipeline.
- Blank source cells become `null` and remain valid.
- Numeric values greater than or equal to zero are valid.
- Negative values produce `PriceUnit must not be negative.` and nonnumeric values produce the existing conversion error format.
- Invalid rows remain in `StockStaging` but are excluded from `Stock`; other valid rows still publish.
- Append `PriceUnit` after the existing 11 enum members so persisted `StockTemplateColumn.CanonicalField` integer values do not change.
- Existing mappings without `PriceUnit` remain valid.
- Keep mapping updates scoped by mapping ID and company ID, with the authenticated username recorded for auditing.
- Do not deactivate other mappings when one mapping is activated.
- The new SQL deployment must accept an already compatible `dbo.Stock.PriceUnit`, reject incompatible existing columns, and add `dbo.StockStaging.PriceUnit` when missing.
- Preserve unrelated working-tree changes and stage only files belonging to each task.
- The current baseline is 90 passing tests. Existing NuGet vulnerability and Mapperly/nullability warnings are known; no new warning category is acceptable.

---

### Task 1: Deterministic mapping activation and deactivation

**Files:**
- Modify: `GiselX.Tests/StockTemplateMappingControllerTests.cs`
- Modify: `GiselX.Tests/StockTemplateMappingServiceTests.cs`
- Modify: `GiselX.Web/Controllers/StockTemplateMappingsController.cs:83`
- Modify: `GiselX.Web/Areas/Admin/Views/StockTemplateMappings/Index.cshtml:16`

**Interfaces:**
- Produces: `POST Activate(int id, int companyId, CancellationToken)`.
- Produces: `POST Deactivate(int id, int companyId, CancellationToken)`.
- Consumes: existing `IStockTemplateMappingService.SetActiveAsync(int, int, bool, string, CancellationToken)`.
- Removes: the MVC action boundary `SetActive(..., bool isActive, ...)` and every Stock mapping form field named `isActive`.

- [ ] **Step 1: Write failing controller tests for both explicit commands**

Extend `CapturingService` with a captured call:

```csharp
public (int Id, int CompanyId, bool IsActive, string Username)? ActiveChange { get; private set; }

public Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
    CancellationToken cancellationToken)
{
    ActiveChange = (id, companyId, isActive, username);
    return Task.FromResult(new ServiceResponse
    {
        Message = isActive ? "Mapping activated." : "Mapping deactivated."
    });
}
```

Add these behavior tests:

```csharp
[Fact]
public async Task Activate_forwards_true_company_mapping_and_authenticated_username()
{
    var service = new CapturingService();
    var controller = CreateController(service);

    var result = await controller.Activate(7, 8, CancellationToken.None);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(StockTemplateMappingsController.Index), redirect.ActionName);
    Assert.Equal(8, redirect.RouteValues!["companyId"]);
    Assert.Equal((7, 8, true, "admin"), service.ActiveChange);
    Assert.Equal("Mapping activated.", controller.TempData["StatusMessage"]);
}

[Fact]
public async Task Deactivate_forwards_false_company_mapping_and_authenticated_username()
{
    var service = new CapturingService();
    var controller = CreateController(service);

    await controller.Deactivate(7, 8, CancellationToken.None);

    Assert.Equal((7, 8, false, "admin"), service.ActiveChange);
    Assert.Equal("Mapping deactivated.", controller.TempData["StatusMessage"]);
}
```

The production mutation caught by these tests is either action forwarding the wrong constant, company, ID, or username.

- [ ] **Step 2: Run the controller tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockTemplateMappingControllerTests
```

Expected: compilation fails because `Activate` and `Deactivate` do not exist.

- [ ] **Step 3: Replace the boolean action with explicit server-owned commands**

Replace `SetActive` in `StockTemplateMappingsController` with:

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

Do not retain a public `SetActive` action that accepts a client boolean.

- [ ] **Step 4: Render only the command that applies to the row**

Replace the Stock mapping toggle form in `Index.cshtml` with:

```cshtml
@if (mapping.IsActive)
{
    <form asp-action="Deactivate" method="post" class="d-inline">
        <input type="hidden" name="id" value="@mapping.Id" />
        <input type="hidden" name="companyId" value="@Model.CompanyId" />
        <button class="btn btn-sm btn-outline-secondary">Deactivate</button>
    </form>
}
else
{
    <form asp-action="Activate" method="post" class="d-inline">
        <input type="hidden" name="id" value="@mapping.Id" />
        <input type="hidden" name="companyId" value="@Model.CompanyId" />
        <button class="btn btn-sm btn-outline-secondary">Activate</button>
    </form>
}
```

Neither form may contain `name="isActive"`.

- [ ] **Step 5: Add failing service regression tests for audit and company isolation**

Add `ActiveChange` capture to the Stock fake repository and port the two established distributor-mapping assertions:

```csharp
[Fact]
public async Task SetActiveAsync_updates_requested_state_and_audit_then_commits()
{
    var (service, repository, transaction) = CreateService();
    repository.Mappings.Add(new StockTemplateMapping { Id = 7, CompanyId = 2, Name = "Daily" });

    var response = await service.SetActiveAsync(7, 2, true, "admin", CancellationToken.None);

    Assert.True(response.IsSuccess);
    var change = Assert.IsType<ActiveChange>(repository.ActiveChange);
    Assert.Equal((7, 2, true, "admin"),
        (change.Id, change.CompanyId, change.IsActive, change.ModifiedBy));
    Assert.InRange(change.ModifiedDate, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    Assert.True(transaction.CommitCalled);
}

[Fact]
public async Task SetActiveAsync_rejects_cross_company_mapping_without_commit()
{
    var (service, repository, transaction) = CreateService();
    repository.Mappings.Add(new StockTemplateMapping { Id = 7, CompanyId = 2, Name = "Daily" });

    var response = await service.SetActiveAsync(7, 99, true, "admin", CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
    Assert.Null(repository.ActiveChange);
    Assert.False(transaction.CommitCalled);
}
```

- [ ] **Step 6: Run focused tests and compile Razor**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~StockTemplateMappingControllerTests|FullyQualifiedName~StockTemplateMappingServiceTests"
dotnet build GiselX.Web\GiselX.Web.csproj --no-restore -p:BaseOutputPath=.tmp\stock-mapping-state\bin\
```

Expected: all focused tests pass and the Razor view compiles.

- [ ] **Step 7: Commit the activation fix**

```powershell
git add GiselX.Tests\StockTemplateMappingControllerTests.cs GiselX.Tests\StockTemplateMappingServiceTests.cs GiselX.Web\Controllers\StockTemplateMappingsController.cs GiselX.Web\Areas\Admin\Views\StockTemplateMappings\Index.cshtml
git commit -m "fix: make stock mapping state commands explicit"
```

---

### Task 2: Optional PriceUnit canonical mapping and parser validation

**Files:**
- Modify: `GiselX.Tests/StockCanonicalFieldCatalogTests.cs`
- Modify: `GiselX.Tests/StockTemplateMappingServiceTests.cs`
- Modify: `GiselX.Tests/StockWorkbookParserTests.cs`
- Modify: `GiselX.Domain/StockCanonicalField.cs`
- Modify: `GiselX.Service/StockCanonicalFieldCatalog.cs`
- Modify: `GiselX.Service.Dto/StockStagingRowDto.cs`
- Modify: `GiselX.Service/StockWorkbookParser.cs`

**Interfaces:**
- Produces: `StockCanonicalField.PriceUnit` with persisted integer value `11`.
- Produces: `StockStagingRowDto.PriceUnit : decimal?`.
- Preserves: `StockCanonicalFieldCatalog.RequiredFields` as exactly ProductId, ProductName, and SaldoAkhir.
- Consumes: existing `StockCellValueConverter` decimal conversion behavior.

- [ ] **Step 1: Write failing catalog and mapping-compatibility tests**

Add:

```csharp
[Fact]
public void PriceUnit_is_appended_optional_and_decimal()
{
    Assert.Equal(11, (int)StockCanonicalField.PriceUnit);
    Assert.Equal(typeof(decimal), StockCanonicalFieldCatalog.GetValueType(StockCanonicalField.PriceUnit));
    Assert.DoesNotContain(StockCanonicalField.PriceUnit, StockCanonicalFieldCatalog.RequiredFields);
}

[Fact]
public async Task SaveAsync_accepts_mapping_without_optional_price_unit()
{
    var (service, repository, transaction) = CreateService();

    var response = await service.SaveAsync(ValidMapping(), "admin", CancellationToken.None);

    Assert.True(response.IsSuccess);
    Assert.DoesNotContain(Assert.Single(repository.Inserted).Columns,
        column => column.CanonicalField == StockCanonicalField.PriceUnit);
    Assert.True(transaction.CommitCalled);
}
```

The explicit enum value assertion protects existing database integer assignments from accidental insertion or reordering.

- [ ] **Step 2: Write failing parser tests for the approved value contract**

Extend the parser fixture so PriceUnit is mapped from column 4, then add:

```csharp
[Fact]
public void Parse_accepts_blank_price_unit_as_null()
{
    var result = ParseWithPriceUnit(CoreRow(3, "4"));

    var row = Assert.Single(result.Rows);
    Assert.True(row.IsValid);
    Assert.Null(row.PriceUnit);
}

[Theory]
[InlineData("0", 0)]
[InlineData("12.50", 12.50)]
public void Parse_accepts_nonnegative_price_unit(string raw, decimal expected)
{
    var result = ParseWithPriceUnit(CoreRow(3, "4").Append((3, 4, raw)).ToArray());

    var row = Assert.Single(result.Rows);
    Assert.True(row.IsValid);
    Assert.Equal(expected, row.PriceUnit);
}

[Fact]
public void Parse_rejects_negative_price_unit_but_keeps_the_row()
{
    var result = ParseWithPriceUnit(CoreRow(3, "4").Append((3, 4, "-0.01")).ToArray());

    var row = Assert.Single(result.Rows);
    Assert.Equal(-0.01m, row.PriceUnit);
    Assert.False(row.IsValid);
    Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.PriceUnit &&
        error.Reason == "PriceUnit must not be negative.");
}

[Fact]
public void Parse_rejects_nonnumeric_price_unit_as_a_conversion_error()
{
    var result = ParseWithPriceUnit(CoreRow(3, "4").Append((3, 4, "free")).ToArray());

    var row = Assert.Single(result.Rows);
    Assert.Null(row.PriceUnit);
    Assert.False(row.IsValid);
    Assert.Contains(result.Errors, error => error.Field == StockCanonicalField.PriceUnit &&
        error.RejectedValue == "free" && error.Reason.Contains("Decimal"));
}
```

Implement `ParseWithPriceUnit` by adding `Column(4, StockCanonicalField.PriceUnit)` to the existing required mapping before calling the real `StockWorkbookParser`.

- [ ] **Step 3: Run all three focused test classes and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~StockCanonicalFieldCatalogTests|FullyQualifiedName~StockTemplateMappingServiceTests|FullyQualifiedName~StockWorkbookParserTests"
```

Expected: compilation fails because `StockCanonicalField.PriceUnit` and `StockStagingRowDto.PriceUnit` do not exist.

- [ ] **Step 4: Append the enum member and decimal catalog case**

Append, without reordering existing members:

```csharp
public enum StockCanonicalField
{
    ProductId,
    ProductName,
    ProductPackaging,
    ProductPcsInCtn,
    ProductNetto,
    ProductUnit,
    SaldoAwal,
    SaldoMasukPO,
    SaldoAkhir,
    BatchNumber,
    ExpiredDate,
    PriceUnit
}
```

Include `StockCanonicalField.PriceUnit` in the decimal arm of `StockCanonicalFieldCatalog.GetValueType`. Do not add it to `RequiredFields` or `GetMaxLength`.

- [ ] **Step 5: Carry and validate PriceUnit in the parser**

Add `public decimal? PriceUnit { get; set; }` to `StockStagingRowDto`. Add PriceUnit cases to both exhaustive parser switches:

```csharp
StockCanonicalField.PriceUnit => row.PriceUnit,
```

and:

```csharp
case StockCanonicalField.PriceUnit: row.PriceUnit = (decimal?)value; break;
```

After the existing SaldoAkhir rule, add:

```csharp
if (row.PriceUnit < 0)
{
    errors.Add(Error(row.SourceRowNumber, StockCanonicalField.PriceUnit,
        row.PriceUnit.Value.ToString(), "PriceUnit must not be negative."));
}
```

Do not add a required-value check; nullable relational comparison keeps blank values valid.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the Step 3 command. Expected: all catalog, mapping, and parser tests pass.

- [ ] **Step 7: Commit the canonical/parser increment**

```powershell
git add GiselX.Tests\StockCanonicalFieldCatalogTests.cs GiselX.Tests\StockTemplateMappingServiceTests.cs GiselX.Tests\StockWorkbookParserTests.cs GiselX.Domain\StockCanonicalField.cs GiselX.Service\StockCanonicalFieldCatalog.cs GiselX.Service.Dto\StockStagingRowDto.cs GiselX.Service\StockWorkbookParser.cs
git commit -m "feat: parse optional stock price unit"
```

---

### Task 3: PriceUnit persistence model and compatibility-safe deployment

**Files:**
- Modify: `GiselX.Tests/StockPersistenceModelTests.cs`
- Modify: `GiselX.Tests/StockManualSqlScriptTests.cs`
- Modify: `GiselX.Domain/Stock.cs`
- Modify: `GiselX.Domain/StockStaging.cs`
- Modify: `GiselX.Web/Context/GiselXDbContext.cs:154`
- Create: `GiselX.Web/Context/Migrations/20260810190000_AddStockPriceUnit.cs`
- Modify: `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`
- Create: `GiselX.Web/DatabaseScripts/20260810190000_AddStockPriceUnit.sql`

**Interfaces:**
- Produces: `Stock.PriceUnit : decimal?` mapped to nullable `decimal(18,2)`.
- Produces: `StockStaging.PriceUnit : decimal?` mapped to nullable `decimal(18,2)`.
- Produces: EF migration ID `20260810190000_AddStockPriceUnit`.
- Produces: a matching manual SQL script that records the same migration ID only after compatibility checks succeed.

- [ ] **Step 1: Write failing EF metadata tests**

Add:

```csharp
[Fact]
public void Model_maps_nullable_price_unit_on_final_and_staging_stock()
{
    using var db = CreateContext();
    var stockProperty = db.Model.FindEntityType(typeof(Stock))!
        .FindProperty(nameof(Stock.PriceUnit))!;
    var stagingProperty = db.Model.FindEntityType(typeof(StockStaging))!
        .FindProperty(nameof(StockStaging.PriceUnit))!;

    Assert.True(stockProperty.IsNullable);
    Assert.True(stagingProperty.IsNullable);
    Assert.Equal("decimal(18, 2)", stockProperty.GetColumnType());
    Assert.Equal("decimal(18, 2)", stagingProperty.GetColumnType());
}
```

- [ ] **Step 2: Write failing manual-deployment contract tests**

Add a `ReadPriceUnitScript()` helper that reads `GiselX.Web/DatabaseScripts/20260810190000_AddStockPriceUnit.sql`, then test the required safety behavior:

```csharp
[Fact]
public void PriceUnit_script_guards_and_validates_both_nullable_decimal_columns()
{
    var sql = ReadPriceUnitScript();

    Assert.Contains("COL_LENGTH(N'dbo.Stock', N'PriceUnit')", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("COL_LENGTH(N'dbo.StockStaging', N'PriceUnit')", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("TYPE_NAME", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("precision", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("scale", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("is_nullable", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("THROW", sql, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void PriceUnit_script_records_history_only_inside_the_transaction()
{
    var sql = ReadPriceUnitScript();

    Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("20260810190000_AddStockPriceUnit", sql, StringComparison.Ordinal);
    Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
    Assert.True(sql.IndexOf("INSERT INTO dbo.__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) <
                sql.IndexOf("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 3: Run persistence and SQL tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~StockPersistenceModelTests|FullyQualifiedName~StockManualSqlScriptTests"
```

Expected: compilation fails on missing domain properties and the new script is absent.

- [ ] **Step 4: Add nullable domain properties and EF configuration**

Add `public decimal? PriceUnit { get; set; }` after `ProductUnit` in both `Stock` and `StockStaging`. Configure both entities with:

```csharp
entity.Property(e => e.PriceUnit).HasColumnType("decimal(18, 2)");
```

Update the model snapshot so both properties are nullable and use `decimal(18,2)`.

- [ ] **Step 5: Add the compatibility-checking EF migration**

Create the self-contained migration in the existing project style:

```csharp
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

[DbContext(typeof(GiselXDbContext))]
[Migration("20260810190000_AddStockPriceUnit")]
public partial class AddStockPriceUnit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.Stock', N'PriceUnit') IS NULL
                ALTER TABLE dbo.Stock ADD PriceUnit decimal(18,2) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
                  AND c.name = N'PriceUnit'
                  AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                       OR c.scale <> 2 OR c.is_nullable <> 1)
            )
                THROW 51000, 'dbo.Stock.PriceUnit must be nullable decimal(18,2).', 1;

            IF COL_LENGTH(N'dbo.StockStaging', N'PriceUnit') IS NULL
                ALTER TABLE dbo.StockStaging ADD PriceUnit decimal(18,2) NULL;
            ELSE IF EXISTS
            (
                SELECT 1 FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
                  AND c.name = N'PriceUnit'
                  AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
                       OR c.scale <> 2 OR c.is_nullable <> 1)
            )
                THROW 51001, 'dbo.StockStaging.PriceUnit must be nullable decimal(18,2).', 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Columns are retained because either column may predate this migration.
    }
}
```

- [ ] **Step 6: Add the matching rerunnable SQL Server script**

Create `20260810190000_AddStockPriceUnit.sql` with this transaction-protected implementation:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'GiselX-20260810190000-AddStockPriceUnit',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 51002, 'Could not acquire the Stock PriceUnit deployment lock.', 1;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51003, 'dbo.__EFMigrationsHistory is required before installing Stock PriceUnit.', 1;

    IF COL_LENGTH(N'dbo.Stock', N'PriceUnit') IS NULL
        ALTER TABLE dbo.Stock ADD PriceUnit decimal(18,2) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U')
          AND c.name = N'PriceUnit'
          AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
               OR c.scale <> 2 OR c.is_nullable <> 1)
    )
        THROW 51000, 'dbo.Stock.PriceUnit must be nullable decimal(18,2).', 1;

    IF COL_LENGTH(N'dbo.StockStaging', N'PriceUnit') IS NULL
        ALTER TABLE dbo.StockStaging ADD PriceUnit decimal(18,2) NULL;
    ELSE IF EXISTS
    (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StockStaging', N'U')
          AND c.name = N'PriceUnit'
          AND (TYPE_NAME(c.user_type_id) <> N'decimal' OR c.precision <> 18
               OR c.scale <> 2 OR c.is_nullable <> 1)
    )
        THROW 51001, 'dbo.StockStaging.PriceUnit must be nullable decimal(18,2).', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20260810190000_AddStockPriceUnit'
    )
    BEGIN
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260810190000_AddStockPriceUnit', N'10.0.8');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

- [ ] **Step 7: Run persistence and SQL tests and verify GREEN**

Run the Step 3 command. Expected: all tests pass.

- [ ] **Step 8: Commit the persistence increment**

```powershell
git add GiselX.Tests\StockPersistenceModelTests.cs GiselX.Tests\StockManualSqlScriptTests.cs GiselX.Domain\Stock.cs GiselX.Domain\StockStaging.cs GiselX.Web\Context\GiselXDbContext.cs GiselX.Web\Context\Migrations\20260810190000_AddStockPriceUnit.cs GiselX.Web\Context\Migrations\GiselXDbContextModelSnapshot.cs GiselX.Web\DatabaseScripts\20260810190000_AddStockPriceUnit.sql
git commit -m "feat: persist stock price unit"
```

---

### Task 4: Stage, publish, map, and query PriceUnit

**Files:**
- Modify: `GiselX.Tests/StockUploadServiceTests.cs`
- Modify: `GiselX.Service/StockUploadService.cs`
- Modify: `GiselX.Repository/StockUploadRepository.cs`
- Modify: `GiselX.Repository/StockRepository.cs`
- Modify: `GiselX.Service.Dto/StockDto.cs`

**Interfaces:**
- Consumes: `StockStagingRowDto.PriceUnit` from Task 2.
- Produces: `StockStaging.PriceUnit` and `Stock.PriceUnit` persistence.
- Produces: `StockDto.PriceUnit : decimal?` for API/list/download serialization.
- Preserves: legacy bulk upload, now using explicit name-based `SqlBulkCopy.ColumnMappings`.

- [ ] **Step 1: Write a failing upload-service propagation test**

Create an analysis row with `PriceUnit = 12.50m` and add:

```csharp
[Fact]
public async Task Upload_copies_price_unit_to_staging_before_publication()
{
    var analysis = Analysis(1, 0);
    analysis.Rows[0].PriceUnit = 12.50m;
    var (service, repository, _) = CreateService(analysis);

    var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
        true, "user", CancellationToken.None);

    Assert.True(response.IsSuccess);
    Assert.Equal(12.50m, Assert.Single(repository.StagingRows).PriceUnit);
    Assert.Equal([3], repository.PublishedSourceRows);
}
```

The existing `Analysis` helper returns a list through `IReadOnlyList`, so mutating the first row's property is valid. Do not mock the copied value separately from the real `StockUploadService.ToStaging` behavior.

- [ ] **Step 2: Run the upload tests and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockUploadServiceTests
```

Expected: compilation fails until the domain/DTO changes from previous tasks exist, then the assertion fails because `ToStaging` does not copy PriceUnit.

- [ ] **Step 3: Copy PriceUnit through staging and set-based publication**

Add `PriceUnit = row.PriceUnit` to `StockUploadService.ToStaging`.

In `StockUploadRepository.InsertStagingAsync`, add `PriceUnit` to both the INSERT column list and VALUES list after `ProductUnit`.

In `PublishValidRowsAsync`, add `PriceUnit` after `ProductUnit` on both sides:

```sql
INSERT INTO Stock (..., ProductUnit, PriceUnit, SaldoAwal, ...)
SELECT ..., COALESCE(s.ProductUnit,''), s.PriceUnit, COALESCE(s.SaldoAwal,0), ...
```

Keep the value nullable; do not `COALESCE` it to zero.

- [ ] **Step 4: Add PriceUnit to the DTO and latest-list query contract**

Add to `StockDto` after `ProductUnit`:

```csharp
[Display(Name = "Price Unit")]
public decimal? PriceUnit { get; set; }
```

Add `nameof(Stock.PriceUnit)` to the `StockRepository.SelectAsync` allowlist. Extend the numeric filter branch to accept nullable decimals:

```csharp
case { } t when t == typeof(decimal) || t == typeof(decimal?):
    if (decimal.TryParse(parameter.Value, out var decimalValue))
    {
        clauses.Add($"s.[{parameter.Key}] = @{parameter.Key}");
        parameters.Add(parameter.Key, decimalValue);
    }
    break;
```

The existing `SELECT s.*` latest and batch queries then hydrate the nullable property automatically.

- [ ] **Step 5: Make legacy SqlBulkCopy mapping name-based**

Add `PriceUnit` to the legacy upload `DataTable` and each row. Before `WriteToServerAsync`, add explicit source/destination mappings for every DataColumn:

```csharp
foreach (DataColumn column in dataTable.Columns)
{
    sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
}
```

This is required because `StockUploadBatchId`, `StockDate`, and `PriceUnit` were appended to the SQL table at different times; positional bulk copy is not safe. Use `DBNull.Value` when nullable PriceUnit has no value.

- [ ] **Step 6: Run the upload tests and build repository/service projects**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockUploadServiceTests
dotnet build GiselX.Repository\GiselX.Repository.csproj --no-restore
dotnet build GiselX.Service\GiselX.Service.csproj --no-restore
```

Expected: focused tests pass and both projects compile without new warnings.

- [ ] **Step 7: Commit the upload/read increment**

```powershell
git add GiselX.Tests\StockUploadServiceTests.cs GiselX.Service\StockUploadService.cs GiselX.Repository\StockUploadRepository.cs GiselX.Repository\StockRepository.cs GiselX.Service.Dto\StockDto.cs
git commit -m "feat: publish and query stock price unit"
```

---

### Task 5: Stock table, batch download, and legacy SQL projections

**Files:**
- Create: `GiselX.Tests/StockDownloadControllerTests.cs`
- Modify: `GiselX.Web/Controllers/StocksController.cs:72`
- Modify: `GiselX.Web/Views/Stocks/Index.cshtml:44`
- Modify: `GiselX.Web/Stock.sql`
- Modify: `GiselX.Web/StoredProcedures/GiselStock_Select.sql`
- Modify: `GiselX.Web/StoredProcedures/GiselStock_SelectByCust.sql`
- Modify: `GiselX.Web/StoredProcedures/GiselStock_SelectByCustPeriod.sql`

**Interfaces:**
- Produces: Stock DataTable property `priceUnit` immediately after `productUnit`.
- Produces: batch workbook column `Price Unit` immediately after `Unit`.
- Preserves: historical rows as blank Excel cells when `PriceUnit` is null.
- Preserves: legacy stored-procedure callers with the same procedure names and an added nullable PriceUnit projection/filter.

- [ ] **Step 1: Write a failing download workbook test against the real controller export**

Create a minimal fake `IStockService` whose `DownloadBatchAsync` returns one complete `StockDto` with `PriceUnit = 12.50m`. Construct `StocksController(fakeService, null!, null!)`, call `DownloadBatch`, open the returned `FileStreamResult.FileStream` using `XLWorkbook`, and assert literal cells:

```csharp
[Fact]
public async Task DownloadBatch_writes_price_unit_after_unit()
{
    var controller = new StocksController(new StockServiceFake(), null!, null!);

    var result = Assert.IsType<FileStreamResult>(
        await controller.DownloadBatch(9, CancellationToken.None));
    using var workbook = new XLWorkbook(result.FileStream);
    var sheet = workbook.Worksheet(1);

    Assert.Equal("Unit", sheet.Cell(1, 6).GetString());
    Assert.Equal("Price Unit", sheet.Cell(1, 7).GetString());
    Assert.Equal(12.50m, sheet.Cell(2, 7).GetValue<decimal>());
    Assert.Equal("Saldo Awal", sheet.Cell(1, 8).GetString());
}
```

Use this complete fake implementation:

```csharp
private sealed class StockServiceFake : IStockService
{
    public Task<ServiceResponse<IEnumerable<StockDto>>> DownloadBatchAsync(long batchId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ServiceResponse<IEnumerable<StockDto>>
        {
            Data =
            [
                new StockDto
                {
                    ProductId = "P-1", ProductName = "Product", ProductPackaging = "Box",
                    ProductPcsInCtn = 12, ProductNetto = 1.5m, ProductUnit = "CTN",
                    PriceUnit = 12.50m, SaldoAwal = 2, SaldoMasukPO = 3, SaldoAkhir = 5,
                    BatchNumber = "B-1", ExpiredDate = new DateTime(2027, 1, 1),
                    CompanyId = 7, CreatedDate = new DateTime(2026, 8, 10),
                    StockUploadBatchId = 9, StockDate = new DateTime(2026, 8, 9)
                }
            ]
        });

    public Task<ServiceResponse> UploadAsync(IEnumerable<StockDto> stocks, int companyId,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ServiceResponse<PagedListDto<StockDto>>> SelectAsync(PagedListRequestDto request,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ServiceResponse<IEnumerable<StockDto>>> SelectByCustPeriodAsync(int companyId, int year,
        int month, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ServiceResponse<IReadOnlyList<StockUploadHistoryOptionDto>>> ListCompletedUploadsAsync(
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
```

- [ ] **Step 2: Run the download test and verify RED**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter FullyQualifiedName~StockDownloadControllerTests
```

Expected: the seventh header is currently `Saldo Awal`, not `Price Unit`.

- [ ] **Step 3: Insert Price Unit into the batch workbook**

Change the header order to:

```csharp
var headers = new[] { "Product ID", "Product Name", "Packaging", "Pcs/Ctn", "Netto", "Unit",
    "Price Unit", "Saldo Awal", "Saldo Masuk PO", "Saldo Akhir", "Batch No", "Expired Date", "Stock Date" };
```

Write `dto.PriceUnit` to cell 7 and shift every existing column from Saldo Awal through Stock Date one cell right. Assign null for blank PriceUnit so historical rows export an empty cell.

- [ ] **Step 4: Add Price Unit to the Stock DataTable**

In `Index.cshtml`, insert `<th>Price Unit</th>` immediately after `<th>Unit</th>`, and insert:

```javascript
{data:'priceUnit'}
```

immediately after `{data:'productUnit'}`. Keep later headers and JavaScript columns in the same order.

- [ ] **Step 5: Update every supported legacy projection**

Add `[PriceUnit]` immediately after `[ProductUnit]` in:

- `Stock.sql`
- `GiselStock_Select.sql` data projection
- `GiselStock_SelectByCust.sql`
- `GiselStock_SelectByCustPeriod.sql`

In `GiselStock_Select.sql`, also add:

```sql
@PriceUnit DECIMAL(18, 2) = NULL,
```

the predicate:

```sql
AND (@PriceUnit IS NULL OR [PriceUnit] = @PriceUnit)
```

to both the data and count queries, and matching ascending/descending sort CASE expressions for `priceUnit`/`PriceUnit`.

- [ ] **Step 6: Run download tests and compile Razor**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~StockDownloadControllerTests|FullyQualifiedName~StockUploadServiceTests"
dotnet build GiselX.Web\GiselX.Web.csproj --no-restore -p:BaseOutputPath=.tmp\stock-priceunit-ui\bin\
```

Expected: workbook assertions pass and the Stock view compiles.

- [ ] **Step 7: Commit display and legacy projections**

```powershell
git add GiselX.Tests\StockDownloadControllerTests.cs GiselX.Web\Controllers\StocksController.cs GiselX.Web\Views\Stocks\Index.cshtml GiselX.Web\Stock.sql GiselX.Web\StoredProcedures\GiselStock_Select.sql GiselX.Web\StoredProcedures\GiselStock_SelectByCust.sql GiselX.Web\StoredProcedures\GiselStock_SelectByCustPeriod.sql
git commit -m "feat: show stock price unit"
```

---

### Task 6: Full regression and deployment verification

**Files:**
- Modify only when a verification-discovered defect first receives a failing regression test.

**Interfaces:**
- Consumes: the complete feature through tests, generated SQL inspection, and manual browser/database checks.

- [ ] **Step 1: Run focused Stock tests**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --filter "FullyQualifiedName~Stock"
```

Expected: all Stock tests pass.

- [ ] **Step 2: Run the complete suite with isolated output**

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\stock-priceunit-final\bin\ --verbosity minimal
```

Expected: zero failures and at least the 90 baseline tests plus the new regressions. Report the already known NuGet/Mapperly/nullability warnings separately; reject any new warning category.

- [ ] **Step 3: Generate and inspect the EF migration range**

```powershell
dotnet ef migrations script 20260810160000_AddStockSnapshotUploads 20260810190000_AddStockPriceUnit --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext --no-build
```

Verify that the generated range only conditionally adds nullable `decimal(18,2)` columns, throws on incompatible existing columns, and records migration history after successful execution. Confirm there is no Stock data rewrite.

- [ ] **Step 4: Manually verify mapping state in both directions**

For one company mapping, click Deactivate and verify the row becomes Inactive with `Mapping deactivated.`. Click Activate and verify the row becomes Active with `Mapping activated.`. Verify another company's mapping cannot be changed by altering posted IDs.

- [ ] **Step 5: Manually verify mixed PriceUnit upload behavior**

Map a workbook Price Unit column and upload rows containing blank, `0`, `12.50`, `-1`, and `free`. Verify blank/zero/positive rows publish with null/zero/positive values, negative/nonnumeric rows remain in staging with the correct row errors, the batch completes with warnings, and the final table excludes invalid rows.

- [ ] **Step 6: Verify listing, filtering, and historical download**

Verify Price Unit appears after Unit in the DataTable, sorts numerically, and an exact numeric column filter works when supplied through the API. Download both a new batch and a historical batch; confirm the new value appears in column G and historical nulls are blank.

- [ ] **Step 7: Verify deployment against both database starting states**

Run the manual script on a schema with neither column and then rerun it unchanged. Separately test a schema where `dbo.Stock.PriceUnit decimal(18,2) NULL` already exists but the staging column does not; confirm the script preserves Stock, adds StockStaging, and records the migration. Change a disposable column to an incompatible definition and verify the script throws and rolls back without inserting migration history.

- [ ] **Step 8: Review the final workspace**

```powershell
git diff --check
git status --short
```

Confirm no unrelated changes, generated workbooks, test uploads, or `.tmp` build artifacts are staged. If verification required a fix, ensure its regression test was observed failing before the production correction.
