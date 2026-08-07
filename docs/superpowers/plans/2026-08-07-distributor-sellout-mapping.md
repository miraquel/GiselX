# Distributor Sellout Template Mapping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add company-specific Excel template mappings that load a complete weekly sellout file into typed staging and atomically replace the corresponding week's final `SalesTransaction` data.

**Architecture:** Keep `SalesTransaction` as the canonical sellout table. Add mapping, upload-batch, and typed-staging entities; parse workbooks through focused ClosedXML services; persist every parsed row before validation; and promote a valid batch through one SQL transaction. Preserve legacy sellout visibility while adding explicit week and combined-month views for new batches.

**Tech Stack:** .NET 10 / C# 14, ASP.NET Core MVC, EF Core 10.0.8 migrations, SQL Server, Dapper 2.1.79, Microsoft.Data.SqlClient 7.0.1, ClosedXML 0.105.0, xUnit.

## Global Constraints

- A company must configure at least one named mapping before its next sellout upload; there is no implicit fixed-template fallback.
- A company submits one complete sellout file per Monday-through-Sunday reporting week.
- A company may own multiple named active mappings; mapping selection is automatic and must resolve to exactly one match.
- Worksheet, header row, and first data row are configured independently per mapping.
- Header matching uses the complete ordered normalized header signature.
- Required canonical fields are exactly `CustomerId`, `CustomerName`, `ProductId`, `ProductName`, `InvoiceNo`, `InvoiceDate`, `InvoiceQty`, and `InvoiceUnit`.
- Optional canonical fields have no fixed or calculated defaults in mapping configuration; unmapped values use existing final-table empty/zero/minimum-date persistence conventions.
- One invalid data row fails the whole batch and leaves current final sellout data unchanged.
- A valid re-upload replaces only the same company's selected reporting week.
- `SalesTransaction` remains the physical final sellout table.
- Preserve the user's existing uncommitted `GiselX.Web/appsettings.json` changes when adding upload configuration.

---

## File Structure

### New domain and persistence files

- `GiselX.Domain/SelloutCanonicalField.cs` — canonical field enum.
- `GiselX.Domain/SelloutUploadBatchStatus.cs` — processing lifecycle enum.
- `GiselX.Domain/DistributorTemplateMapping.cs` — company mapping aggregate root.
- `GiselX.Domain/DistributorTemplateColumn.cs` — source-to-canonical assignment.
- `GiselX.Domain/SelloutUploadBatch.cs` — upload attempt and promotion status.
- `GiselX.Domain/SelloutStaging.cs` — nullable typed canonical staging row.
- `GiselX.Web/Context/Migrations/` — generated `AddDistributorSelloutMapping` migration and snapshot update.

### New DTO and contract files

- `GiselX.Service.Dto/DistributorTemplateMappingDto.cs`
- `GiselX.Service.Dto/DistributorTemplateColumnDto.cs`
- `GiselX.Service.Dto/SaveDistributorTemplateMappingDto.cs`
- `GiselX.Service.Dto/SelloutWorkbookPreviewDto.cs`
- `GiselX.Service.Dto/SelloutMappingDetectionResultDto.cs`
- `GiselX.Service.Dto/SelloutUploadResultDto.cs`
- `GiselX.Service.Dto/SelloutUploadBatchDto.cs`
- `GiselX.Service.Dto/SelloutStagingErrorDto.cs`
- `GiselX.Repository.Interface/IDistributorTemplateMappingRepository.cs`
- `GiselX.Repository.Interface/ISelloutUploadRepository.cs`
- `GiselX.Service.Interface/IDistributorTemplateMappingService.cs`
- `GiselX.Service.Interface/ISelloutWorkbookInspector.cs`
- `GiselX.Service.Interface/ISelloutUploadService.cs`

### New implementation files

- `GiselX.Service/SelloutCanonicalFieldCatalog.cs` — required/type/length metadata.
- `GiselX.Service/SelloutHeaderNormalizer.cs` — deterministic signature creation.
- `GiselX.Service/DistributorTemplateMappingService.cs` — mapping validation and orchestration.
- `GiselX.Service/ClosedXmlSelloutWorkbookInspector.cs` — sheets, preview, and auto-detection.
- `GiselX.Service/SelloutCellValueConverter.cs` — explicit typed cell conversion.
- `GiselX.Service/SelloutParseResult.cs` — parsed staging rows plus flattened errors.
- `GiselX.Service/SelloutWorkbookParser.cs` — mapped-row construction and validation.
- `GiselX.Service/SelloutUploadService.cs` — batch lifecycle and commit boundary.
- `GiselX.Repository/DistributorTemplateMappingRepository.cs` — Dapper mapping CRUD.
- `GiselX.Repository/SelloutUploadRepository.cs` — batch/staging persistence and promotion.

### New web files

- `GiselX.Web/Models/DistributorTemplateMappingEditViewModel.cs`
- `GiselX.Web/Models/SelloutUploadViewModel.cs`
- `GiselX.Web/Controllers/DistributorTemplateMappingsController.cs`
- `GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Edit.cshtml`
- `GiselX.Web/Views/SalesTransactions/UploadResult.cshtml`
- `GiselX.Web/StoredProcedures/GiselSales_SelectByCustWeek.sql`
- `GiselX.Web/StoredProcedures/GiselSales_SelectByCustPeriod.sql`

### New tests

- `GiselX.Tests/GiselX.Tests.csproj`
- `GiselX.Tests/SelloutCanonicalFieldCatalogTests.cs`
- `GiselX.Tests/SelloutHeaderNormalizerTests.cs`
- `GiselX.Tests/SelloutPersistenceModelTests.cs`
- `GiselX.Tests/DistributorTemplateMappingServiceTests.cs`
- `GiselX.Tests/ClosedXmlSelloutWorkbookInspectorTests.cs`
- `GiselX.Tests/SelloutWorkbookParserTests.cs`
- `GiselX.Tests/SelloutUploadServiceTests.cs`
- `GiselX.Tests/SalesTransactionVisibilityTests.cs`

---

### Task 1: Test Harness, Canonical Field Catalog, and Header Normalization

**Files:**
- Create: `GiselX.Tests/GiselX.Tests.csproj`
- Modify: `GiselX.sln`
- Create: `GiselX.Domain/SelloutCanonicalField.cs`
- Create: `GiselX.Service/SelloutCanonicalFieldCatalog.cs`
- Create: `GiselX.Service/SelloutHeaderNormalizer.cs`
- Test: `GiselX.Tests/SelloutCanonicalFieldCatalogTests.cs`
- Test: `GiselX.Tests/SelloutHeaderNormalizerTests.cs`

**Interfaces:**
- Produces: `SelloutCanonicalField` with all 24 existing `SalesTransaction` data fields.
- Produces: `SelloutCanonicalFieldCatalog.RequiredFields`, `GetValueType(field)`, and `GetMaxLength(field)`.
- Produces: `SelloutHeaderNormalizer.Normalize(string)` and `CreateSignature(IEnumerable<string>)`.

- [ ] **Step 1: Scaffold the xUnit project and add references**

Run:

```powershell
dotnet new xunit -n GiselX.Tests -f net10.0
dotnet add GiselX.Tests/GiselX.Tests.csproj reference GiselX.Domain/GiselX.Domain.csproj GiselX.Service/GiselX.Service.csproj GiselX.Service.Dto/GiselX.Service.Dto.csproj GiselX.Repository.Interface/GiselX.Repository.Interface.csproj GiselX.Web/GiselX.Web.csproj
dotnet sln GiselX.sln add GiselX.Tests/GiselX.Tests.csproj
```

Delete the generated `GiselX.Tests/UnitTest1.cs` using the normal patch workflow.

- [ ] **Step 2: Write failing catalog and normalizer tests**

```csharp
using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

public class SelloutCanonicalFieldCatalogTests
{
    [Fact]
    public void RequiredFields_AreExactlyTheApprovedCoreFields()
    {
        var expected = new HashSet<SelloutCanonicalField>
        {
            SelloutCanonicalField.CustomerId,
            SelloutCanonicalField.CustomerName,
            SelloutCanonicalField.ProductId,
            SelloutCanonicalField.ProductName,
            SelloutCanonicalField.InvoiceNo,
            SelloutCanonicalField.InvoiceDate,
            SelloutCanonicalField.InvoiceQty,
            SelloutCanonicalField.InvoiceUnit
        };

        Assert.Equal(expected, SelloutCanonicalFieldCatalog.RequiredFields.ToHashSet());
    }

    [Fact]
    public void Catalog_ContainsEveryCanonicalField()
    {
        Assert.All(Enum.GetValues<SelloutCanonicalField>(), field =>
            Assert.NotNull(SelloutCanonicalFieldCatalog.GetValueType(field)));
    }
}

public class SelloutHeaderNormalizerTests
{
    [Theory]
    [InlineData(" Invoice   No ", "INVOICE NO")]
    [InlineData("customer\tname", "CUSTOMER NAME")]
    public void Normalize_TrimsCollapsesWhitespaceAndIgnoresCase(string input, string expected)
    {
        Assert.Equal(expected, SelloutHeaderNormalizer.Normalize(input));
    }

    [Fact]
    public void CreateSignature_IsOrderedAndDropsTrailingBlanks()
    {
        Assert.Equal("CUSTOMER ID\u001FINVOICE NO",
            SelloutHeaderNormalizer.CreateSignature([" Customer Id ", "Invoice No", "", "  "]));
    }
}
```

- [ ] **Step 3: Run the tests and verify the missing types fail compilation**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "SelloutCanonicalFieldCatalogTests|SelloutHeaderNormalizerTests"`

Expected: FAIL because `SelloutCanonicalField`, `SelloutCanonicalFieldCatalog`, and `SelloutHeaderNormalizer` do not exist.

- [ ] **Step 4: Implement the enum, exact catalog, and normalizer**

Define all fields in the same canonical order as `SalesTransaction`:

```csharp
public enum SelloutCanonicalField
{
    CustomerId, CustomerName, CustomerAlias, CustomerAddress,
    ProductId, ProductBrand, ProductName, ProductPackaging, ProductPcsInCtn,
    BatchNumber, ExpiredDate, InvoiceNo, InvoiceDate, InvoiceQty, InvoiceUnit,
    UnitPrice, GrossValue, DiscountValue, DiscountPct, NetValue, InKg,
    GeisaPOId, ShipDate, SalesmanNameGMK
}
```

Implement `RequiredFields` as the exact eight-value set in the test. `GetValueType` returns `typeof(string)` for text, `typeof(int)` for `ProductPcsInCtn`, `typeof(decimal)` for quantity/value fields, and `typeof(DateTime)` for date fields. `GetMaxLength` mirrors the lengths configured in `GiselXDbContext`; numeric/date fields return `null`.

Implement signatures with `Regex.Replace(header.Trim(), @"\s+", " ").ToUpperInvariant()` joined by the unit separator `\u001F`. Remove only trailing normalized blank headers; reject internal blank or duplicate normalized headers through `ValidateHeaders` returning explicit messages.

- [ ] **Step 5: Run the focused tests**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter "SelloutCanonicalFieldCatalogTests|SelloutHeaderNormalizerTests"`

Expected: PASS.

- [ ] **Step 6: Commit the canonical metadata unit**

```powershell
git add GiselX.sln GiselX.Tests GiselX.Domain/SelloutCanonicalField.cs GiselX.Service/SelloutCanonicalFieldCatalog.cs GiselX.Service/SelloutHeaderNormalizer.cs
git commit -m "test: establish sellout mapping field metadata"
```

---

### Task 2: Persistence Entities and EF Core Migration

**Files:**
- Create: `GiselX.Domain/SelloutUploadBatchStatus.cs`
- Create: `GiselX.Domain/DistributorTemplateMapping.cs`
- Create: `GiselX.Domain/DistributorTemplateColumn.cs`
- Create: `GiselX.Domain/SelloutUploadBatch.cs`
- Create: `GiselX.Domain/SelloutStaging.cs`
- Modify: `GiselX.Domain/Company.cs`
- Modify: `GiselX.Domain/SalesTransaction.cs`
- Modify: `GiselX.Web/Context/GiselXDbContext.cs`
- Modify: `GiselX.Web/Context/Migrations/GiselXDbContextModelSnapshot.cs`
- Create via EF: migration files under `GiselX.Web/Context/Migrations/` named for `AddDistributorSelloutMapping`
- Test: `GiselX.Tests/SelloutPersistenceModelTests.cs`

**Interfaces:**
- Produces: the four persisted aggregates and navigation relationships used by all repository tasks.
- Produces: nullable `SalesTransaction.SelloutUploadBatchId` and `SalesTransaction.ReportingWeekStart`.
- Produces: unique/index/check constraints described below.

- [ ] **Step 1: Write the failing EF model test**

```csharp
using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public class SelloutPersistenceModelTests
{
    private static GiselXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GiselXDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXModelTest;Trusted_Connection=True")
            .Options;
        return new GiselXDbContext(options);
    }

    [Fact]
    public void Model_DefinesMappingBatchStagingAndWeeklySalesIndex()
    {
        using var db = CreateContext();
        Assert.NotNull(db.Model.FindEntityType(typeof(DistributorTemplateMapping)));
        Assert.NotNull(db.Model.FindEntityType(typeof(DistributorTemplateColumn)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SelloutUploadBatch)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SelloutStaging)));

        var sales = db.Model.FindEntityType(typeof(SalesTransaction))!;
        Assert.Contains(sales.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(["CompanyId", "ReportingWeekStart"]));
    }
}
```

- [ ] **Step 2: Run the model test and verify it fails**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutPersistenceModelTests`

Expected: FAIL because the persistence entities do not exist.

- [ ] **Step 3: Add the entities and navigation properties**

Use `long` keys for batch and staging rows and `int` keys for mappings and mapping columns. Define:

```csharp
public enum SelloutUploadBatchStatus { Processing, Failed, Completed, Replaced }

public sealed class DistributorTemplateMapping
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public string HeaderSignature { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public Company Company { get; set; } = null!;
    public ICollection<DistributorTemplateColumn> Columns { get; set; } = [];
    public ICollection<SelloutUploadBatch> UploadBatches { get; set; } = [];
}
```

`DistributorTemplateColumn` contains mapping ID, source column number/header, and `SelloutCanonicalField`. `SelloutUploadBatch` contains the fields and statuses in the spec. `SelloutStaging` contains nullable versions of all 24 canonical fields plus `SourceRowNumber`, `IsValid`, and `ValidationErrorsJson` initialized to `"[]"`.

Add `Company.DistributorTemplateMappings`, `Company.SelloutUploadBatches`, nullable batch/week fields on `SalesTransaction`, and matching navigations.

- [ ] **Step 4: Configure EF mappings and database constraints**

Add `DbSet` properties and fluent configuration with:

- unique index on `DistributorTemplateMapping(CompanyId, Name)`;
- unique indexes on `DistributorTemplateColumn(MappingId, SourceColumnNumber)` and `(MappingId, CanonicalField)`;
- check constraints `HeaderRowNumber > 0`, `FirstDataRowNumber > HeaderRowNumber`, and Monday week start using `DATEDIFF(day,'19000101',ReportingWeekStart) % 7 = 0`;
- filtered unique index on `SelloutUploadBatch(CompanyId, ReportingWeekStart)` where status is `Completed`;
- index on `SelloutStaging(SelloutUploadBatchId, SourceRowNumber)`;
- index on `SalesTransaction(CompanyId, ReportingWeekStart)`;
- `DeleteBehavior.Restrict` from company/mapping/batch history and cascade only from batch to staging.

Map enums as integers, `ReportingWeekStart` as `date`, validation JSON as `nvarchar(max)`, and canonical staging lengths/decimal types to the same limits as `SalesTransaction`.

- [ ] **Step 5: Generate and inspect the migration**

Run:

```powershell
dotnet ef migrations add AddDistributorSelloutMapping --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext --output-dir Context/Migrations
dotnet ef migrations script --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext --idempotent --output artifacts/AddDistributorSelloutMapping.sql
```

Inspect `artifacts/AddDistributorSelloutMapping.sql` and confirm it creates all four tables, two new `SalesTransaction` columns, foreign keys, checks, and indexes without dropping existing data. Do not commit the generated `artifacts` file.

- [ ] **Step 6: Run the model test and build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutPersistenceModelTests
dotnet build GiselX.sln
```

Expected: PASS and build succeeds.

- [ ] **Step 7: Commit the persistence schema**

```powershell
git add GiselX.Domain GiselX.Web/Context GiselX.Tests/SelloutPersistenceModelTests.cs
git commit -m "feat: add sellout mapping and staging schema"
```

---

### Task 3: Mapping DTOs, Repository, and Validation Service

**Files:**
- Create: `GiselX.Service.Dto/DistributorTemplateMappingDto.cs`
- Create: `GiselX.Service.Dto/DistributorTemplateColumnDto.cs`
- Create: `GiselX.Service.Dto/SaveDistributorTemplateMappingDto.cs`
- Create: `GiselX.Repository.Interface/IDistributorTemplateMappingRepository.cs`
- Create: `GiselX.Repository/DistributorTemplateMappingRepository.cs`
- Create: `GiselX.Service.Interface/IDistributorTemplateMappingService.cs`
- Create: `GiselX.Service/DistributorTemplateMappingService.cs`
- Test: `GiselX.Tests/DistributorTemplateMappingServiceTests.cs`

**Interfaces:**
- Produces repository methods `GetByCompanyAsync`, `GetActiveByCompanyAsync`, `GetByIdAsync`, `InsertAsync`, `UpdateAsync`, and `SetActiveAsync`.
- Produces service methods `ListAsync`, `GetAsync`, `SaveAsync`, and `SetActiveAsync`.
- Consumes: canonical catalog and signatures from Task 1; entities from Task 2.

- [ ] **Step 1: Define the DTO and repository contracts**

```csharp
public sealed class SaveDistributorTemplateMappingDto
{
    public int? Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public string HeaderSignature { get; set; } = string.Empty;
    public IReadOnlyList<DistributorTemplateColumnDto> Columns { get; set; } = [];
}

public sealed class DistributorTemplateColumnDto
{
    public int SourceColumnNumber { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public SelloutCanonicalField CanonicalField { get; set; }
}

public sealed class DistributorTemplateMappingDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public string HeaderSignature { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IReadOnlyList<DistributorTemplateColumnDto> Columns { get; set; } = [];
}

public interface IDistributorTemplateMappingRepository
{
    Task<IReadOnlyList<DistributorTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DistributorTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<DistributorTemplateMapping?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<int> InsertAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken);
    Task UpdateAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken);
}
```

The service contract wraps results in existing `ServiceResponse<T>` and accepts the current username for audit fields.

- [ ] **Step 2: Write failing mapping-service tests with an in-memory fake repository**

Cover these named tests:

```csharp
[Fact] public async Task SaveAsync_RejectsMissingRequiredFields()
[Fact] public async Task SaveAsync_RejectsDuplicateSourceColumns()
[Fact] public async Task SaveAsync_RejectsDuplicateCanonicalFields()
[Fact] public async Task SaveAsync_RejectsFirstDataRowNotAfterHeader()
[Fact] public async Task SaveAsync_RejectsDuplicateCompanyMappingName()
[Fact] public async Task SaveAsync_PersistsValidMappingAndCommits()
```

For the required-fields assertion, omit `InvoiceUnit` and verify a 400 response containing `InvoiceUnit`. For the valid test, include all eight core fields and assert `InsertAsync` is called once and the fake transaction's `Commit()` flag is true.

- [ ] **Step 3: Run the tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter DistributorTemplateMappingServiceTests`

Expected: FAIL because service and contracts are missing.

- [ ] **Step 4: Implement validation and Dapper persistence**

`SaveAsync` must perform these checks in order and return all errors together:

1. nonblank name and worksheet;
2. positive header row;
3. first data row greater than header row;
4. nonblank signature;
5. no duplicate source column numbers;
6. no duplicate canonical fields;
7. all eight `RequiredFields` present;
8. no other mapping with the same normalized name in the company.

Use one Dapper query with `QueryMultipleAsync` to load a mapping and ordered columns. Insert the mapping, retrieve its identity, then insert all columns inside the existing scoped `IDbTransaction`. Update replaces column assignments only after all validation succeeds. Commit in the service after repository success.

- [ ] **Step 5: Run focused tests and build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter DistributorTemplateMappingServiceTests
dotnet build GiselX.sln
```

Expected: PASS.

- [ ] **Step 6: Commit mapping persistence and validation**

```powershell
git add GiselX.Service.Dto GiselX.Repository.Interface/IDistributorTemplateMappingRepository.cs GiselX.Repository/DistributorTemplateMappingRepository.cs GiselX.Service.Interface/IDistributorTemplateMappingService.cs GiselX.Service/DistributorTemplateMappingService.cs GiselX.Tests/DistributorTemplateMappingServiceTests.cs
git commit -m "feat: persist and validate distributor mappings"
```

---

### Task 4: Workbook Inspection and Automatic Mapping Detection

**Files:**
- Modify: `GiselX.Service/GiselX.Service.csproj`
- Create: `GiselX.Service.Dto/SelloutWorkbookPreviewDto.cs`
- Create: `GiselX.Service.Dto/SelloutMappingDetectionResultDto.cs`
- Create: `GiselX.Service.Interface/ISelloutWorkbookInspector.cs`
- Create: `GiselX.Service/ClosedXmlSelloutWorkbookInspector.cs`
- Test: `GiselX.Tests/ClosedXmlSelloutWorkbookInspectorTests.cs`
- Modify: `GiselX.Tests/GiselX.Tests.csproj`

**Interfaces:**
- Produces: `InspectAsync(byte[] workbook, string? worksheetName, int headerRow, int firstDataRow, CancellationToken)`.
- Produces: `DetectAsync(byte[] workbook, IReadOnlyCollection<DistributorTemplateMappingDto>, CancellationToken)` returning `SelloutMappingDetectionResultDto` with exactly one mapping or a stable error code.
- Consumes: signature normalization from Task 1 and mapping DTOs from Task 3.

- [ ] **Step 1: Add ClosedXML 0.105.0 to the service and test projects**

Run:

```powershell
dotnet add GiselX.Service/GiselX.Service.csproj package ClosedXML --version 0.105.0
dotnet add GiselX.Tests/GiselX.Tests.csproj package ClosedXML --version 0.105.0
```

- [ ] **Step 2: Write in-memory workbook tests**

Create workbooks with `XLWorkbook`, save to `MemoryStream`, and assert:

```csharp
[Fact] public async Task InspectAsync_ReturnsWorksheetNamesAndConfiguredPreview()
[Fact] public async Task InspectAsync_RejectsMissingWorksheet()
[Fact] public async Task InspectAsync_RejectsDuplicateNormalizedHeaders()
[Fact] public async Task DetectAsync_ReturnsOnlyExactOrderedSignatureMatch()
[Fact] public async Task DetectAsync_RejectsNoMatch()
[Fact] public async Task DetectAsync_RejectsMultipleMatches()
```

The exact-match test must include two mappings and prove that reordered headers do not match. The preview test must put headers on row 3 and data on row 6 to prove header/data configuration is independent.

- [ ] **Step 3: Run the inspector tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter ClosedXmlSelloutWorkbookInspectorTests`

Expected: FAIL because inspector types are missing.

- [ ] **Step 4: Implement one-open workbook inspection**

`InspectAsync` returns worksheet names, normalized ordered headers with one-based column numbers, the computed signature, and at most ten preview rows beginning at `firstDataRow`. It rejects unreadable/non-OpenXML content with a stable user message.

`DetectAsync` opens the workbook once, loops over active mappings, locates each configured worksheet/header row, and compares exact signatures. Return error code `TemplateNotConfigured` for zero matches and `TemplateAmbiguous` plus matching names for multiple matches.

Define the result explicitly:

```csharp
public sealed class SelloutMappingDetectionResultDto
{
    public DistributorTemplateMappingDto? Mapping { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> MatchingMappingNames { get; init; } = [];
    public bool IsSuccess => Mapping is not null && ErrorCode is null;
}
```

- [ ] **Step 5: Run focused tests**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter ClosedXmlSelloutWorkbookInspectorTests`

Expected: PASS.

- [ ] **Step 6: Commit workbook inspection**

```powershell
git add GiselX.Service/GiselX.Service.csproj GiselX.Service.Dto/SelloutWorkbookPreviewDto.cs GiselX.Service.Interface/ISelloutWorkbookInspector.cs GiselX.Service/ClosedXmlSelloutWorkbookInspector.cs GiselX.Tests
git commit -m "feat: inspect and detect distributor workbooks"
```

---

### Task 5: Typed Staging Row Parsing and Validation

**Files:**
- Create: `GiselX.Service.Dto/SelloutStagingErrorDto.cs`
- Create: `GiselX.Service/SelloutCellValueConverter.cs`
- Create: `GiselX.Service/SelloutParseResult.cs`
- Create: `GiselX.Service/SelloutWorkbookParser.cs`
- Test: `GiselX.Tests/SelloutWorkbookParserTests.cs`

**Interfaces:**
- Produces: `SelloutWorkbookParser.Parse(byte[] workbook, DistributorTemplateMappingDto mapping, DateTime reportingWeekStart)` returning `SelloutParseResult`.
- Consumes: mapping DTOs, canonical metadata, and `SelloutStaging`.

- [ ] **Step 1: Write failing parser tests**

Build workbook fixtures and cover:

```csharp
[Fact] public void Parse_UsesConfiguredSheetHeaderAndFirstDataRow()
[Fact] public void Parse_IgnoresCompletelyBlankRows()
[Fact] public void Parse_ReportsEveryMissingCoreValueWithSourceRow()
[Fact] public void Parse_RejectsInvoiceDateOutsideSelectedWeek()
[Fact] public void Parse_AllowsUnmappedOptionalFields()
[Fact] public void Parse_RejectsInvalidNonblankOptionalDecimal()
[Fact] public void Parse_RejectsStringsLongerThanCanonicalLimit()
[Fact] public void Parse_RejectsWorkbookWithNoDataRows()
```

The source-row test must assert errors contain `SourceRowNumber = 7`, `Field = InvoiceNo`, the rejected value, and a stable reason. The selected-week fixture uses Monday `2026-08-03`, accepts `2026-08-09`, and rejects `2026-08-10`.

- [ ] **Step 2: Run the parser tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutWorkbookParserTests`

Expected: FAIL because parser classes are absent.

- [ ] **Step 3: Implement explicit cell conversion**

For each mapped field, use the catalog type:

- strings use `GetFormattedString().Trim()`;
- integers accept typed Excel numbers or invariant integer text;
- decimals accept typed Excel numbers, invariant text, then `id-ID` text;
- dates accept typed Excel dates, Excel serial dates, invariant text, then `id-ID` text;
- blank optional values return `null` without error;
- nonblank conversion failures return one `SelloutStagingErrorDto` and never silently become zero or a sentinel.

Do not use reflection to assign values. Use an exhaustive `switch (SelloutCanonicalField)` so compiler-visible assignments cover all 24 fields.

- [ ] **Step 4: Implement row and batch validation**

For each nonblank data row:

1. convert mapped cells;
2. check all eight core staged values;
3. require `InvoiceQty > 0` to preserve existing sellout validation;
4. require `InvoiceDate >= weekStart && InvoiceDate < weekStart.AddDays(7)`;
5. enforce canonical string lengths;
6. serialize that row's errors with `System.Text.Json` into `ValidationErrorsJson`;
7. set `IsValid` only when the row has no errors.

Return a batch error when no nonblank rows exist.

Use this exact result boundary:

```csharp
public sealed record SelloutParseResult(
    IReadOnlyList<SelloutStaging> Rows,
    IReadOnlyList<SelloutStagingErrorDto> Errors,
    string? BatchError);
```

- [ ] **Step 5: Run focused tests**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutWorkbookParserTests`

Expected: PASS.

- [ ] **Step 6: Commit typed staging parsing**

```powershell
git add GiselX.Service.Dto/SelloutStagingErrorDto.cs GiselX.Service/SelloutCellValueConverter.cs GiselX.Service/SelloutWorkbookParser.cs GiselX.Tests/SelloutWorkbookParserTests.cs
git commit -m "feat: parse distributor rows into typed staging"
```

---

### Task 6: Upload Batch Persistence and Atomic Weekly Promotion

**Files:**
- Create: `GiselX.Service.Dto/SelloutUploadResultDto.cs`
- Create: `GiselX.Service.Dto/SelloutUploadBatchDto.cs`
- Create: `GiselX.Repository.Interface/ISelloutUploadRepository.cs`
- Create: `GiselX.Repository/SelloutUploadRepository.cs`
- Create: `GiselX.Service.Interface/ISelloutUploadService.cs`
- Create: `GiselX.Service/SelloutUploadService.cs`
- Test: `GiselX.Tests/SelloutUploadServiceTests.cs`

**Interfaces:**
- Produces service method `UploadAsync(Stream file, string originalFileName, int companyId, DateTime reportingWeekStart, string uploadedBy, CancellationToken)`.
- Produces service method `GetBatchAsync(long batchId, int companyId, CancellationToken)` for result display.
- Produces repository batch/staging/promotion operations.
- Consumes: active mappings, automatic detection, parser, domain schema, and scoped `IDbTransaction`.

- [ ] **Step 1: Define repository and service contracts**

```csharp
public interface ISelloutUploadRepository
{
    Task<long> InsertBatchAsync(SelloutUploadBatch batch, CancellationToken cancellationToken);
    Task InsertStagingAsync(IReadOnlyCollection<SelloutStaging> rows, CancellationToken cancellationToken);
    Task MarkFailedAsync(long batchId, int totalRows, int invalidRows, string errorSummary, CancellationToken cancellationToken);
    Task PromoteAsync(long batchId, CancellationToken cancellationToken);
    Task<(SelloutUploadBatch Batch, IReadOnlyList<SelloutStaging> Rows)?> GetBatchAsync(long batchId, int companyId, CancellationToken cancellationToken);
}

public interface ISelloutUploadService
{
    Task<ServiceResponse<SelloutUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        int companyId, DateTime reportingWeekStart, string uploadedBy, CancellationToken cancellationToken);
    Task<ServiceResponse<SelloutUploadBatchDto>> GetBatchAsync(long batchId, int companyId,
        CancellationToken cancellationToken);
}
```

`SelloutUploadResultDto` contains `BatchId`, `Status`, `TotalRows`, `ValidRows`, `InvalidRows`, `Message`, and `IReadOnlyList<SelloutStagingErrorDto> Errors`. `SelloutUploadBatchDto` adds company, mapping name, reporting week, filename, hash, uploader, timestamps, and the same error collection for the result page.

- [ ] **Step 2: Write failing orchestration tests with fakes**

Cover:

```csharp
[Fact] public async Task UploadAsync_RejectsNonMondayBeforeWorkbookProcessing()
[Fact] public async Task UploadAsync_PersistsFailedDetectionBatchWithoutPromotion()
[Fact] public async Task UploadAsync_PersistsEveryStagedRowAndCommitsFailedValidation()
[Fact] public async Task UploadAsync_PromotesAndCommitsValidBatch()
[Fact] public async Task UploadAsync_DoesNotCommitWhenPromotionThrows()
```

The invalid-row test asserts `InsertStagingAsync` receives valid and invalid rows, `MarkFailedAsync` is called, `PromoteAsync` is not called, and the transaction commits to preserve diagnostics. The valid test asserts exactly one promotion call after staging persistence.

- [ ] **Step 3: Run the service tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutUploadServiceTests`

Expected: FAIL because upload contracts are missing.

- [ ] **Step 4: Implement repository persistence**

Use Dapper for batch insert/update and `SqlBulkCopy` for staging rows. Set explicit column mappings rather than relying on ordinal position. `PromoteAsync` must execute inside the injected transaction in this order:

```sql
DECLARE @CompanyId int, @WeekStart date;
SELECT @CompanyId = CompanyId, @WeekStart = ReportingWeekStart
FROM SelloutUploadBatch WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @BatchId AND Status = 0;

UPDATE SelloutUploadBatch
SET Status = 3
WHERE CompanyId = @CompanyId
  AND ReportingWeekStart = @WeekStart
  AND Status = 2;

DELETE s
FROM SelloutStaging s
JOIN SelloutUploadBatch b ON b.Id = s.SelloutUploadBatchId
WHERE b.CompanyId = @CompanyId
  AND b.ReportingWeekStart = @WeekStart
  AND b.Status = 3;

DELETE FROM SalesTransaction
WHERE CompanyId = @CompanyId
  AND (ReportingWeekStart = @WeekStart
       OR (ReportingWeekStart IS NULL
           AND InvoiceDate >= @WeekStart
           AND InvoiceDate < DATEADD(day, 7, @WeekStart)));
```

Then `INSERT ... SELECT` valid staging rows into every final `SalesTransaction` column. Convert nullable optional values with `COALESCE` to existing empty string, zero, and `1753-01-01` conventions; set batch ID, week start, company ID, and one common upload timestamp. Finally update the new batch to `Completed` with total/valid counts. Throw if the batch is missing, not `Processing`, or contains invalid staging rows.

- [ ] **Step 5: Implement upload orchestration**

Read the file once into a bounded byte array while computing SHA-256. Enforce `.xlsx`, the configured byte limit, Monday week start, and nonempty content. Insert `Processing` batch metadata before detection. Detect, parse, bulk-stage, and either:

- mark failed and commit diagnostics; or
- call `PromoteAsync` and commit the replacement.

Return batch ID and flattened errors in both cases. Do not catch promotion exceptions as success; leave the transaction uncommitted so disposal rolls it back.

- [ ] **Step 6: Run focused tests and build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutUploadServiceTests
dotnet build GiselX.sln
```

Expected: PASS.

- [ ] **Step 7: Commit batch persistence and promotion**

```powershell
git add GiselX.Service.Dto GiselX.Repository.Interface/ISelloutUploadRepository.cs GiselX.Repository/SelloutUploadRepository.cs GiselX.Service.Interface/ISelloutUploadService.cs GiselX.Service/SelloutUploadService.cs GiselX.Tests/SelloutUploadServiceTests.cs
git commit -m "feat: stage and atomically promote weekly sellout batches"
```

---

### Task 7: Admin Mapping Management UI

**Files:**
- Create: `GiselX.Web/Models/DistributorTemplateMappingEditViewModel.cs`
- Create: `GiselX.Web/Controllers/DistributorTemplateMappingsController.cs`
- Create: `GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Edit.cshtml`
- Modify: `GiselX.Web/Controllers/CompaniesController.cs`
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml`
- Test: `GiselX.Tests/DistributorTemplateMappingControllerTests.cs`

**Interfaces:**
- Produces admin routes under `/Admin/DistributorTemplateMappings`.
- Consumes mapping service and workbook inspector.

- [ ] **Step 1: Write failing controller tests**

Cover:

```csharp
[Fact] public async Task Inspect_ReturnsWorksheetAndHeaderPreview()
[Fact] public async Task Save_ReturnsEditorWithValidationErrors()
[Fact] public async Task Save_RedirectsToCompanyEditOnSuccess()
[Fact] public async Task SetActive_ChangesOnlyMappingOwnedByRouteCompany()
```

Use fake services and assert the controller has `[Area("Admin")]`, Companies Edit authorization, `[ValidateAntiForgeryToken]` on mutations, and returns `BadRequest` when route company ID differs from the submitted company ID.

- [ ] **Step 2: Run controller tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter DistributorTemplateMappingControllerTests`

Expected: FAIL because controller and view model are absent.

- [ ] **Step 3: Implement controller actions**

Add:

```csharp
[Area("Admin")]
[Authorize(PermissionConstants.Companies.Edit)]
public sealed class DistributorTemplateMappingsController : Controller
{
    [HttpGet] public async Task<IActionResult> Create(int companyId, CancellationToken cancellationToken);
    [HttpGet] public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Inspect(IFormFile sampleFile, string? worksheetName,
        int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DistributorTemplateMappingEditViewModel model,
        IFormFile? sampleFile, CancellationToken cancellationToken);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int companyId, int id, bool isActive,
        CancellationToken cancellationToken);
}
```

Create requires a sample. Edit loads stored headers/assignments; a new sample is required only when worksheet/header/data layout or column assignments change. Re-inspect the posted sample during save rather than trusting a client-supplied signature.

- [ ] **Step 4: Build the mapping editor**

Render mapping name, sample upload, worksheet selector, header row, first data row, ten-row preview, and one canonical-field dropdown per source column. Mark the eight required options visibly. JavaScript keeps the selected browser `File` object and posts it as `FormData` whenever worksheet/header/data settings change, so no sample binary is stored server-side.

The save payload contains source column number/header and selected enum value. Disable already-selected canonical fields in other dropdowns and still validate everything server-side.

- [ ] **Step 5: Add the mapping list to Edit Company**

In both Company Edit GET and invalid POST paths, load mappings into `ViewData["SelloutTemplateMappings"]`. Render a table with name, sheet, header row, first data row, status, Edit, Activate/Deactivate, and Add Mapping controls. Do not add a physical Delete action.

- [ ] **Step 6: Run tests and render-check the views**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter DistributorTemplateMappingControllerTests
dotnet build GiselX.sln
```

Start the application and inspect Create/Edit mapping at desktop and narrow widths. Confirm preview columns scroll horizontally rather than expanding the page.

- [ ] **Step 7: Commit mapping administration UI**

```powershell
git add GiselX.Web/Models/DistributorTemplateMappingEditViewModel.cs GiselX.Web/Controllers/DistributorTemplateMappingsController.cs GiselX.Web/Areas/Admin/Views/DistributorTemplateMappings/Edit.cshtml GiselX.Web/Controllers/CompaniesController.cs GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml GiselX.Tests/DistributorTemplateMappingControllerTests.cs
git commit -m "feat: add company sellout mapping administration"
```

---

### Task 8: Weekly Upload UI and Failed Batch Details

**Files:**
- Create: `GiselX.Web/Models/SelloutUploadViewModel.cs`
- Modify: `GiselX.Web/Controllers/SalesTransactionsController.cs`
- Modify: `GiselX.Web/Views/SalesTransactions/Index.cshtml`
- Create: `GiselX.Web/Views/SalesTransactions/UploadResult.cshtml`
- Test: `GiselX.Tests/SalesTransactionsUploadControllerTests.cs`

**Interfaces:**
- Replaces fixed-column controller parsing with `ISelloutUploadService.UploadAsync`.
- Produces GET `UploadResult(long batchId)` scoped to the current company.

- [ ] **Step 1: Write failing upload-controller tests**

Cover:

```csharp
[Fact] public async Task Upload_RejectsMissingFile()
[Fact] public async Task Upload_RejectsNonMondayWeekStart()
[Fact] public async Task Upload_PassesCurrentCompanyAndUsernameToService()
[Fact] public async Task Upload_RedirectsFailedBatchToUploadResult()
[Fact] public async Task UploadResult_CannotReadAnotherCompanyBatch()
```

- [ ] **Step 2: Run tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SalesTransactionsUploadControllerTests`

Expected: FAIL because the controller still uses direct fixed-column parsing.

- [ ] **Step 3: Replace direct upload parsing**

Remove the 24-column `XLWorkbook` mapping from `SalesTransactionsController.Upload`. Resolve the current user, pass the uploaded stream, original filename, company ID, selected week start, and username to `ISelloutUploadService`, then redirect to `UploadResult` for both validation failure and success. Keep authorization and anti-forgery protection.

Remove `DownloadTemplate` and its button because templates must be configured as named company mappings.

- [ ] **Step 4: Update the upload form and result page**

Add a required date input named `ReportingWeekStart` and `.xlsx` file input. Client-side code warns when the date is not Monday, but server validation remains authoritative. When no active mappings exist, disable upload and display: `No active sellout template mapping is configured for your company. Contact an administrator.`

`UploadResult.cshtml` displays batch status, mapping, reporting week, filename, counts, batch error summary, and a table of source row, field, rejected value, and reason. Show success only for `Completed`.

- [ ] **Step 5: Run focused tests and build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SalesTransactionsUploadControllerTests
dotnet build GiselX.sln
```

Expected: PASS.

- [ ] **Step 6: Commit weekly upload UI**

```powershell
git add GiselX.Web/Models/SelloutUploadViewModel.cs GiselX.Web/Controllers/SalesTransactionsController.cs GiselX.Web/Views/SalesTransactions GiselX.Tests/SalesTransactionsUploadControllerTests.cs
git commit -m "feat: add mapped weekly sellout upload experience"
```

---

### Task 9: Week and Month Sellout Queries and Downloads

**Files:**
- Modify: `GiselX.Repository.Interface/ISalesTransactionRepository.cs`
- Modify: `GiselX.Repository/SalesTransactionRepository.cs`
- Modify: `GiselX.Service.Interface/ISalesTransactionService.cs`
- Modify: `GiselX.Service/SalesTransactionService.cs`
- Modify: `GiselX.Web/Controllers/Api/SalesTransactionsController.cs`
- Modify: `GiselX.Web/Controllers/SalesTransactionsController.cs`
- Modify: `GiselX.Web/Views/SalesTransactions/Index.cshtml`
- Modify: `GiselX.Web/StoredProcedures/GiselSales_Select.sql`
- Create: `GiselX.Web/StoredProcedures/GiselSales_SelectByCustWeek.sql`
- Create: `GiselX.Web/StoredProcedures/GiselSales_SelectByCustPeriod.sql`
- Modify: `GiselX.Web/StoredProcedures/GiselSales_SelectPeriods.sql`
- Test: `GiselX.Tests/SalesTransactionVisibilityTests.cs`

**Interfaces:**
- Adds `SelectByCustWeekAsync(int companyId, DateTime weekStart, CancellationToken)`.
- Keeps monthly `SelectByCustPeriodAsync` but changes it to combine active explicit weeks.
- Extends paged selection with `Filters.ViewMode` (`week` or `month`) and `Filters.WeekStart`.

- [ ] **Step 1: Write visibility-rule tests**

Extract period inclusion into a testable `SalesTransactionVisibility` predicate builder or in-memory specification and cover:

```csharp
[Fact] public void WeekView_IncludesOnlyTaggedSelectedWeek()
[Fact] public void MonthView_IncludesAllTaggedWeeksIntersectingInvoiceMonth()
[Fact] public void LegacyMonthView_UsesLatestLegacyCreatedDate()
[Fact] public void ExplicitWeek_SuppressesLegacyRowsForSameCompanyWeek()
[Fact] public void Visibility_NeverCrossesCompanyBoundary()
```

- [ ] **Step 2: Run visibility tests and verify they fail**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SalesTransactionVisibilityTests`

Expected: FAIL because explicit weekly visibility does not exist.

- [ ] **Step 3: Update repository and service query contracts**

Parse `Filters.ViewMode` and `Filters.WeekStart` separately from reflected `SalesTransaction` filters. Require Monday for week mode and year/month for month mode. Add week download methods through repository, service, and MVC controller. Keep company scoping derived from the authenticated user.

- [ ] **Step 4: Rewrite the paged stored procedure visibility predicate**

Add parameters:

```sql
@ViewMode nvarchar(10) = 'month',
@WeekStart date = NULL
```

Materialize visible IDs into `#VisibleSalesIds` once so data and count queries use identical rules:

```sql
INSERT #VisibleSalesIds (Id)
SELECT s.Id
FROM SalesTransaction s
WHERE s.CompanyId = @CompanyId
  AND (
    (@ViewMode = 'week' AND (
      s.ReportingWeekStart = @WeekStart
      OR (s.ReportingWeekStart IS NULL
          AND s.InvoiceDate >= @WeekStart
          AND s.InvoiceDate < DATEADD(day, 7, @WeekStart)
          AND NOT EXISTS (
              SELECT 1 FROM SalesTransaction tagged
              WHERE tagged.CompanyId = s.CompanyId
                AND tagged.ReportingWeekStart = @WeekStart)
          AND s.CreatedDate = (
              SELECT MAX(legacy.CreatedDate) FROM SalesTransaction legacy
              WHERE legacy.CompanyId = s.CompanyId
                AND legacy.ReportingWeekStart IS NULL
                AND legacy.InvoiceDate >= @WeekStart
                AND legacy.InvoiceDate < DATEADD(day, 7, @WeekStart))))
    OR
    (@ViewMode = 'month'
      AND YEAR(s.InvoiceDate) = @Year
      AND MONTH(s.InvoiceDate) = @Month
      AND (s.ReportingWeekStart IS NOT NULL
           OR (s.CreatedDate = (
                 SELECT MAX(legacy.CreatedDate) FROM SalesTransaction legacy
                 WHERE legacy.CompanyId = s.CompanyId
                   AND legacy.ReportingWeekStart IS NULL
                   AND YEAR(legacy.InvoiceDate) = @Year
                   AND MONTH(legacy.InvoiceDate) = @Month)
               AND NOT EXISTS (
                 SELECT 1 FROM SalesTransaction tagged
                 WHERE tagged.CompanyId = s.CompanyId
                   AND tagged.ReportingWeekStart IS NOT NULL
                   AND s.InvoiceDate >= tagged.ReportingWeekStart
                   AND s.InvoiceDate < DATEADD(day, 7, tagged.ReportingWeekStart))))));
```

Join the existing filters, sort, pagination, and count against `#VisibleSalesIds`. Reject invalid `@ViewMode` and a non-Monday/missing `@WeekStart` with `THROW`.

- [ ] **Step 5: Add matching week/month download procedures**

`GiselSales_SelectByCustWeek.sql` uses the same week visibility rule without paging. `GiselSales_SelectByCustPeriod.sql` uses the same month visibility rule without paging. Include `SelloutUploadBatchId` and `ReportingWeekStart` in repository mapping but keep the exported 24 business columns unchanged.

- [ ] **Step 6: Add both filters to the page**

Add a Month/Week segmented control. Month mode shows existing year/month selectors; week mode shows a Monday date picker. DataTables and downloads send the selected mode and period. Switching modes clears the old table request and reloads only after the new period is complete.

- [ ] **Step 7: Run tests and build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SalesTransactionVisibilityTests
dotnet build GiselX.sln
```

Expected: PASS.

- [ ] **Step 8: Commit dual-period querying**

```powershell
git add GiselX.Repository.Interface/ISalesTransactionRepository.cs GiselX.Repository/SalesTransactionRepository.cs GiselX.Service.Interface/ISalesTransactionService.cs GiselX.Service/SalesTransactionService.cs GiselX.Web/Controllers GiselX.Web/Views/SalesTransactions/Index.cshtml GiselX.Web/StoredProcedures GiselX.Tests/SalesTransactionVisibilityTests.cs
git commit -m "feat: query sellout by week or combined month"
```

---

### Task 10: Dependency Registration, Upload Limits, and Migration Deployment Check

**Files:**
- Modify: `GiselX.Repository/ServiceCollectionExtensions.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs`
- Create: `GiselX.Service/SelloutUploadOptions.cs`
- Modify carefully: `GiselX.Web/Program.cs`
- Modify carefully: `GiselX.Web/appsettings.json`
- Test: `GiselX.Tests/SelloutDependencyRegistrationTests.cs`

**Interfaces:**
- Registers every repository/service from Tasks 3–6.
- Binds `SelloutUpload:MaxFileSizeBytes` with default `20971520` (20 MiB).

- [ ] **Step 1: Write the failing DI smoke test**

Build a `ServiceCollection`, register configuration with a test SQL connection string, call both extension methods, and assert descriptors exist for:

```csharp
IDistributorTemplateMappingRepository
ISelloutUploadRepository
IDistributorTemplateMappingService
ISelloutWorkbookInspector
ISelloutUploadService
SelloutWorkbookParser
```

- [ ] **Step 2: Run the DI test and verify it fails**

Run: `dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutDependencyRegistrationTests`

Expected: FAIL because registrations are absent.

- [ ] **Step 3: Register dependencies and bind options**

Add scoped repositories and services. Define:

```csharp
public sealed class SelloutUploadOptions
{
    public const string SectionName = "SelloutUpload";
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
}
```

Bind in `Program.cs` with `builder.Services.Configure<SelloutUploadOptions>(...)`. Merge this setting into `appsettings.json` without overwriting the user's existing uncommitted changes:

```json
"SelloutUpload": {
  "MaxFileSizeBytes": 20971520
}
```

- [ ] **Step 4: Run DI tests, full migration script generation, and build**

Run:

```powershell
dotnet test GiselX.Tests/GiselX.Tests.csproj --filter SelloutDependencyRegistrationTests
dotnet ef migrations script --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext --idempotent --output artifacts/GiselX-idempotent.sql
dotnet build GiselX.sln
```

Expected: PASS; script generation contains new schema and no destructive drop of legacy sellout columns.

- [ ] **Step 5: Commit registrations and configuration while excluding unrelated settings**

Review `git diff -- GiselX.Web/appsettings.json` line by line, stage only the new `SelloutUpload` section plus intentional files, then commit:

```powershell
git add GiselX.Repository/ServiceCollectionExtensions.cs GiselX.Service/ServiceCollectionExtensions.cs GiselX.Service/SelloutUploadOptions.cs GiselX.Web/Program.cs GiselX.Tests/SelloutDependencyRegistrationTests.cs
git add -p GiselX.Web/appsettings.json
git commit -m "chore: register sellout mapping pipeline"
```

---

### Task 11: End-to-End Regression and Acceptance Verification

**Files:**
- Modify as defects require: only files introduced or intentionally changed in Tasks 1–10
- Test: all files under `GiselX.Tests/`
- Reference: `docs/superpowers/specs/2026-08-07-distributor-sellout-mapping-design.md`

**Interfaces:**
- Verifies the completed feature as one workflow; produces no new feature scope.

- [ ] **Step 1: Run the complete automated suite**

Run:

```powershell
dotnet test GiselX.sln --no-restore
dotnet build GiselX.sln --no-restore
```

Expected: all tests pass and build has zero errors.

- [ ] **Step 2: Apply the migration to a disposable SQL Server database**

Set `ConnectionStrings__DefaultConnection` to a disposable database, then run:

```powershell
dotnet ef database update --project GiselX.Web --startup-project GiselX.Web --context GiselXDbContext
```

Verify the four tables, constraints, foreign keys, filtered index, and two nullable `SalesTransaction` columns exist. Run every modified `GiselSales_*` script against that database.

- [ ] **Step 3: Verify mapping management manually**

Create a company mapping from a sample workbook with a non-first worksheet, headers on row 3, and data beginning on row 6. Confirm:

- save is blocked with one required mapping missing;
- save succeeds with all eight core mappings;
- the mapping appears on Edit Company;
- deactivate blocks detection;
- reactivate restores detection;
- a mapping with upload history has no Delete action.

- [ ] **Step 4: Verify failure is all-or-nothing**

Upload a valid file for Monday `2026-08-03`; record final row count. Upload a replacement with one `InvoiceDate` of `2026-08-10`. Confirm the new batch is `Failed`, its staging rows and row/field errors are visible, and the earlier final count and values remain unchanged.

- [ ] **Step 5: Verify successful replacement isolation**

Upload a corrected replacement for `2026-08-03`. Confirm:

- the prior batch is `Replaced`;
- prior successful staging rows are removed;
- final rows for that company/week equal the new file exactly;
- another week and another company are unchanged;
- uploading the same file again still performs full replacement rather than duplicate insertion.

- [ ] **Step 6: Verify week/month UI and downloads**

Create at least two completed weekly batches in August 2026. Confirm week view/download returns only its selected batch and month view/download returns both. Confirm a legacy month still uses its latest legacy `CreatedDate` and that legacy rows are hidden only for a week replaced by mapped data.

- [ ] **Step 7: Run repository hygiene checks**

Run:

```powershell
git diff --check
git status --short
```

Confirm no generated workbook, migration-script artifact, connection string, uploaded data file, or unrelated `appsettings.json` change is staged.

- [ ] **Step 8: Commit only verification fixes**

If verification required code corrections, stage those exact files and commit:

```powershell
git commit -m "fix: complete sellout mapping acceptance checks"
```

If no corrections were needed, do not create an empty commit.
