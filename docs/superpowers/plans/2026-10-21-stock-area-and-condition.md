# Stock Area and Condition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every Stock row carries an Area (resolved against a per-company master) and a Good/Bad Condition, both read from mapped columns. A Stock upload replaces only the areas it contains.

**Architecture:**
- Area and Condition become two optional canonical fields. The parser keeps their raw text, and `StockWorkbookAnalysisService` resolves them to `StockAreaId` / `StockCondition` before anything is written, so the preview shows the result.
- Publication scopes its delete to the areas carried by the upload's valid rows.
- Every read path ranks per `(CompanyId, area)` instead of per company.

**Tech Stack:** .NET 10, ASP.NET Core MVC, Dapper, EF Core migrations (schema authority only), SQL Server, xUnit with hand-rolled fakes.

**Spec:** `docs/superpowers/specs/2026-10-21-stock-area-and-condition-design.md`

## Global Constraints

- **Condition storage.** `StockCondition` is `Good = 0`, `Bad = 1`, stored as `tinyint`, `NOT NULL DEFAULT 0` on `Stock` and `StockStaging`.
- **Area storage.** `StockAreaId` is `int NULL`, FK to `StockArea(Id)`, `ON DELETE NO ACTION`. Null means "no area".
- **`StockArea` columns.**
  - `Name nvarchar(100)` and `Aliases nvarchar(1000) NULL`; aliases are `;`-delimited.
  - `IsActive bit`.
  - Audit columns `CreatedDate`, `CreatedBy`, `ModifiedDate`, `ModifiedBy`.
- **`StockStaging` raw text.** `SourceAreaText nvarchar(200) NULL` and `SourceConditionText nvarchar(200) NULL`.
- **`StockTemplateMapping` condition lists.** `GoodConditionValues nvarchar(500) NULL` and `BadConditionValues nvarchar(500) NULL`, both `;`-delimited.
- **Canonical field numbering.** `StockCanonicalField.Area` and `StockCanonicalField.Condition` are appended **after** `SmallestUnitQty`. Existing values are stored as ints in `StockTemplateColumn.CanonicalField` and must not shift.
- **Normalisation** for both area and condition matching: trim, collapse internal whitespace to one space, compare `OrdinalIgnoreCase`.
- **Editor defaults.** `GOOD;GS;BAIK` (Good) and `BAD;BS;BAD STOCK;RUSAK` (Bad). These are the defaults the template editor pre-fills, not values hard-coded into resolution.
- **Error copy.** The row-error reasons are fixed text:
  - `Area '<text>' is not registered for <company name>.`
  - `Area is empty.`
  - `Condition is empty.`
  - `Condition '<text>' is neither a Good nor a Bad value of template <template name>.`
- **Hand-written SQL** brackets `[identity]` if it ever touches Identity tables (CLAUDE.md). Any statement in the manual script that references a column added by the same script goes through `sp_executesql`.
- **Sellout code is not touched.**

## Review Focus

1. **Area text that differs only in case or spacing.** `" medan "`, `"MEDAN"` and `"Medan"` must resolve to the same area. This is pinned in Task 3.
2. **An alias that collides with another area's name or alias in the same company.** It must be rejected at save, never resolved ambiguously at upload. This is pinned in Task 4.
3. **A batch that carried two areas and has one of them replaced.** It must stay live with its remaining rows, and the grid must still show them. This is pinned in Task 7, by SQL shape, and in Task 9 by real-database verification.
4. **A company that registers areas but whose template does not map `Area`.** The upload must be refused at preview with the template's name, not published with null areas. This is pinned in Task 6.
5. **A Condition cell holding a value in neither list**, for example `ED` or `Expired`. The row must be invalid with the value quoted, never counted as Good. This is pinned in Task 3.

---

### Task 1: Storage — `StockArea`, new columns, migration and manual script

**Files:**
- Create: `GiselX.Domain/StockArea.cs`, `GiselX.Domain/StockCondition.cs`
- Modify:
  - `GiselX.Domain/Stock.cs`, `GiselX.Domain/StockStaging.cs`
  - `GiselX.Domain/StockTemplateMapping.cs`, `GiselX.Domain/Company.cs`
  - `GiselX.Web/Context/GiselXDbContext.cs` (Stock region, around lines 249-306)
- Create (generated): `GiselX.Web/Context/Migrations/<id>_AddStockAreaAndCondition.cs` (+ `.Designer.cs`, snapshot)
- Create: `GiselX.Web/DatabaseScripts/<id>_AddStockAreaAndCondition.sql`
- Test: `GiselX.Tests/StockAreaPersistenceModelTests.cs`, `GiselX.Tests/StockAreaManualSqlScriptTests.cs`

**Interfaces:**
- Produces:
  - `StockArea { int Id; int CompanyId; string Name; string? Aliases; bool IsActive; DateTime CreatedDate; string CreatedBy; DateTime? ModifiedDate; string? ModifiedBy; Company Company }`.
  - `enum StockCondition : byte { Good = 0, Bad = 1 }`.
  - `Stock.StockAreaId (int?)`, `Stock.Condition (StockCondition)`, `[NotMapped] Stock.AreaName (string?)`.
  - `StockStaging.StockAreaId`, `StockStaging.Condition`, `StockStaging.SourceAreaText`, `StockStaging.SourceConditionText`.
  - `StockTemplateMapping.GoodConditionValues`, `StockTemplateMapping.BadConditionValues` (both `string?`).
  - `Company.StockAreas` (`ICollection<StockArea>`).

- [ ] **Step 1: Write the failing model tests**

Follow the pattern of `StockPersistenceModelTests`: build a `GiselXDbContext` on a localdb connection string and inspect `db.Model` only. Tests in `StockAreaPersistenceModelTests`:
  - `Stock_area_is_company_owned_with_a_unique_name_per_company`: FK `CompanyId` restrict; unique index on `(CompanyId, Name)`; `Name` max length 100; `Aliases` max length 1000 and nullable.
  - `Stock_and_staging_rows_reference_an_optional_area_without_cascade`: `StockAreaId` nullable on both, FK to `StockArea` with `DeleteBehavior.Restrict` / `NoAction`.
  - `Condition_is_a_non_null_tinyint_defaulting_to_good`: on both entities, column type `tinyint`, `HasDefaultValue(StockCondition.Good)`.
  - `Staging_keeps_the_raw_area_and_condition_text`: both columns max length 200, nullable.
  - `Template_mapping_keeps_good_and_bad_condition_lists`: both max length 500, nullable.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~StockAreaPersistenceModelTests`
Expected: compile failure (`StockArea` / `StockCondition` not defined).

- [ ] **Step 3: Add the entities, properties and `OnModelCreating` configuration to make the model tests pass**

`Condition` uses `.HasConversion<byte>().HasColumnType("tinyint").HasDefaultValue(StockCondition.Good)`. Add an index on `Stock(StockAreaId)` and on `StockStaging(StockAreaId)`.

- [ ] **Step 4: Generate the migration**

Run: `dotnet ef migrations add AddStockAreaAndCondition --project GiselX.Web`

Check `Up` and `Down`:
- `Up` creates `StockArea`, adds the six columns plus the two template columns, and adds the FKs and indexes.
- `Down` reverses all of it.
- No `migrationBuilder.Sql` is needed.

- [ ] **Step 5: Write the failing manual-script tests**

`StockAreaManualSqlScriptTests` locates `DatabaseScripts/*_AddStockAreaAndCondition.sql` by walking up to `GiselX.sln`. It asserts that the script contains:
  - `SET XACT_ABORT ON`, `BEGIN TRANSACTION`, `sp_getapplock`
  - `OBJECT_ID(N'dbo.StockArea', N'U') IS NULL`
  - a `COL_LENGTH` guard for each of the eight new columns
  - `DEFAULT (0)` for both `Condition` columns
  - `INSERT INTO dbo.__EFMigrationsHistory` with the migration id read from the migration file name

It also asserts that the script does **not** contain `DROP TABLE`, `DELETE FROM` or `TRUNCATE`.

- [ ] **Step 6: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~StockAreaManualSqlScriptTests`
Expected: FAIL (file not found).

- [ ] **Step 7: Write the manual script**

Mirror `20260907024804_AddStockStagingSheetSource.sql`: a transaction, an applock resource `GiselX-<id>-AddStockAreaAndCondition`, prerequisite `THROW`s, a guard per object, and the history insert. Use the `ProductVersion` string the generated `.Designer.cs` carries.

- [ ] **Step 8: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 9: Verify against SQL Server**

Run `dotnet ef database update --project GiselX.Web` against the dev database. Then, on a database restored to the preceding migration, run the manual script twice with `sqlcmd -S localhost -d <db> -E -C -i <script>`. Expected:
- both runs succeed;
- the schema matches the EF result (columns, FKs, indexes, defaults);
- existing `Stock` rows read `Condition = 0`, `StockAreaId = NULL`.

- [ ] **Step 10: Commit**

```bash
git add GiselX.Domain GiselX.Web/Context GiselX.Web/DatabaseScripts GiselX.Tests/StockArea*Tests.cs
git commit -m "feat: store an area and a good/bad condition on every Stock row"
```

---

### Task 2: Parser — read Area and Condition as raw text

**Files:**
- Modify:
  - `GiselX.Domain/StockCanonicalField.cs`
  - `GiselX.Service/StockCanonicalFieldCatalog.cs`
  - `GiselX.Service/StockWorkbookParser.cs` (`Assign`, `GetValue`)
  - `GiselX.Service.Dto/StockStagingRowDto.cs`
- Test: `GiselX.Tests/StockWorkbookParserTests.cs`

**Interfaces:**
- Produces:
  - `StockCanonicalField.Area`, `StockCanonicalField.Condition`, appended in that order.
  - `StockStagingRowDto.SourceAreaText (string?)`, `SourceConditionText (string?)`, `StockAreaId (int?)`, `Condition (StockCondition, default Good)`.
  - Catalog: both fields are text, max length 200, and **not** required.

- [ ] **Step 1: Write the failing tests**
  - `Mapped_area_and_condition_cells_are_kept_as_raw_text`: a sheet whose Area column holds `" Medan "` and whose Condition column holds `"BS"` yields `SourceAreaText == " Medan "` and `SourceConditionText == "BS"`, untrimmed. Resolution normalises later.
  - `A_template_without_area_or_condition_leaves_both_null`.
  - `Area_and_condition_are_not_required_fields`: `StockCanonicalFieldCatalog.RequiredFields` contains neither.
  - `Existing_canonical_field_numbers_are_unchanged`: `(int)StockCanonicalField.SmallestUnitQty == 13`, `(int)Area == 14`, `(int)Condition == 15`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~StockWorkbookParserTests`
Expected: compile failure on the new enum members.

- [ ] **Step 3: Implement**

Add the enum members and the catalog entries. In the parser, `Assign` and `GetValue` map `Area` to `SourceAreaText` and `Condition` to `SourceConditionText`. The max-length validation then covers them through the existing loop.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Commit**

`feat: read the area and condition columns of a Stock workbook`

---

### Task 3: Resolution — area matcher and condition resolver (pure)

**Files:**
- Create: `GiselX.Service/StockAreaMatcher.cs`, `GiselX.Service/StockConditionResolver.cs`
- Test: `GiselX.Tests/StockAreaMatcherTests.cs`, `GiselX.Tests/StockConditionResolverTests.cs`

**Interfaces:**
- Produces:
  - `public static class StockTextNormalizer { public static string Normalize(string? value); public static IReadOnlyList<string> SplitList(string? delimited); }` lives in `StockAreaMatcher.cs`. `SplitList` splits on `;`, normalises each entry, drops blanks and de-duplicates case-insensitively.
  - `public sealed class StockAreaMatcher(IEnumerable<StockArea> activeAreas, string companyName) { public StockAreaResolution Resolve(string? cellText); }`.
  - `public sealed record StockAreaResolution(int? AreaId, string? Error);`
  - `public static class StockConditionResolver { public static (StockCondition? Condition, string? Error) Resolve(string? cellText, string? goodValues, string? badValues, string templateName); }`

- [ ] **Step 1: Write the failing tests**

`StockAreaMatcherTests`, with areas `{1 "Medan", aliases "MDN; Gdg Medan"}` and `{2 "Binjai"}`:
  - `Name_matches_ignoring_case_and_spacing`: `" medan "` gives 1, and `"MEDAN"` gives 1.
  - `Alias_matches`: `"gdg  medan"` gives 1, and `"mdn"` gives 1.
  - `Unregistered_text_is_an_error_naming_text_and_company`: `"Tebing"` gives an error equal to `Area 'Tebing' is not registered for PT Primatrans.`
  - `Blank_is_an_error`: `"  "` gives `Area is empty.`
  - `Text_matching_two_areas_is_an_error_not_a_guess`: two areas that share an alias give an error containing `more than one area`, with `AreaId` null.

`StockConditionResolverTests`, with good `"GOOD;GS"` and bad `"BAD;BS;BAD STOCK"`:
  - `Good_and_bad_values_resolve_ignoring_case_and_spacing`: `" gs "` gives Good, and `"bad  stock"` gives Bad.
  - `Unknown_value_is_an_error_quoting_it`: `"ED"` gives error `Condition 'ED' is neither a Good nor a Bad value of template Primatrans Stock.`
  - `Blank_is_an_error_never_good`: `""` gives `Condition is empty.` and a `null` condition.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StockAreaMatcherTests|FullyQualifiedName~StockConditionResolverTests"`
Expected: compile failure.

- [ ] **Step 3: Implement to the signatures above**

The matcher builds a dictionary once, from each normalised name and alias to the set of area ids, so a key that maps to more than one area produces the ambiguity error.

- [ ] **Step 4: Run them to verify they pass**

- [ ] **Step 5: Commit**

`feat: resolve Stock area and condition text`

---

### Task 4: Area master — repository, service and admin screens

**Files:**
- Create:
  - `GiselX.Repository.Interface/IStockAreaRepository.cs`, `GiselX.Repository/StockAreaRepository.cs`
  - `GiselX.Service.Interface/IStockAreaService.cs`, `GiselX.Service/StockAreaService.cs`
  - `GiselX.Service.Dto/StockAreaDto.cs`
  - `GiselX.Web/Controllers/StockAreasController.cs`
  - `GiselX.Web/Areas/Admin/Views/StockAreas/Index.cshtml`, `GiselX.Web/Areas/Admin/Views/StockAreas/Edit.cshtml`
- Modify:
  - `GiselX.Repository/ServiceCollectionExtensions.cs`, `GiselX.Service/ServiceCollectionExtensions.cs` (register both)
  - `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml` (a "Stock areas" link to `StockAreas/Index?companyId=`)
- Test: `GiselX.Tests/StockAreaServiceTests.cs`

**Interfaces:**
- Produces:
  - `IStockAreaRepository`:
    - `Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly, CancellationToken)`
    - `Task<StockArea?> GetAsync(int id, int companyId, CancellationToken)`
    - `Task<int> InsertAsync(StockArea, CancellationToken)`
    - `Task UpdateAsync(StockArea, CancellationToken)`
    - `Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken)`
    - `Task<bool> DeleteAsync(int id, int companyId, CancellationToken)`, which returns false when `Stock` or `StockStaging` references the area.
  - `IStockAreaService`: `ListAsync(int companyId, CancellationToken)`, `GetAsync(int id, int companyId, …)`, `SaveAsync(StockAreaDto, string username, …)`, `SetActiveAsync(…)`, `DeleteAsync(…)`. All return `ServiceResponse`, and every write calls `transaction.Commit()`.
  - `StockAreaDto { int Id; int CompanyId; string Name; string? Aliases; bool IsActive; }`.
  - Controller: `[Area("Admin")]`, every action `[Authorize(PermissionConstants.Companies.Edit)]`, shaped like `BusinessUnitsController` with `companyId` carried on every route.

- [ ] **Step 1: Write the failing service tests**

Use a fake `IStockAreaRepository` and a fake transaction, as in other service tests.
  - `Name_is_required_and_at_most_100_characters`.
  - `Aliases_are_split_trimmed_and_deduplicated_before_storing`: `" MDN ;mdn;; Gdg Medan "` is stored as `"MDN;Gdg Medan"`.
  - `An_alias_equal_to_another_active_area_name_or_alias_is_rejected`: the error names the clashing text and the other area.
  - `A_duplicate_name_in_the_same_company_is_rejected_but_allowed_in_another_company`.
  - `An_area_referenced_by_stock_cannot_be_deleted`: the repository returns false, which gives a 409 with the message `This area has stock rows and cannot be deleted. Deactivate it instead.`
  - `Saving_commits_the_transaction`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~StockAreaServiceTests`
Expected: compile failure.

- [ ] **Step 3: Implement the repository (inline Dapper SQL), the service, the DI registration, the controller, the views and the Company edit link**

Normalisation uses `StockTextNormalizer` from Task 3. The stored `Aliases` is `string.Join(";", SplitList(input))`, or null when empty.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Verify in the browser**

Run: `dotnet run --project GiselX.Web`. As an Administrator:
- open a company, follow "Stock areas", and add `Medan` with aliases `MDN`;
- add `Binjai`;
- try adding alias `mdn` to Binjai and see the rejection;
- deactivate and then reactivate an area.

- [ ] **Step 6: Commit**

`feat: manage each distributor's Stock areas`

---

### Task 5: Template condition values — storage, validation and editor

**Files:**
- Modify:
  - `GiselX.Service.Dto/StockTemplateMappingDto.cs`, `GiselX.Service.Dto/SaveStockTemplateMappingDto.cs`
  - `GiselX.Service/StockTemplateMappingService.cs` (`Validate`, `Apply`, insert path, `ToDto`)
  - `GiselX.Repository/StockTemplateMappingRepository.cs` (insert/update SQL and selects)
  - `GiselX.Service/StockWorkbookAnalysisService.cs` (`ToDto` copies both lists)
  - `GiselX.Web/Areas/Admin/Views/StockTemplateMappings/Edit.cshtml`
- Test: `GiselX.Tests/StockTemplateMappingServiceTests.cs` (create it if absent)

**Interfaces:**
- Produces: `GoodConditionValues` and `BadConditionValues` (`string?`) on both DTOs, stored normalised via `StockTextNormalizer.SplitList` and joined with `;`.

- [ ] **Step 1: Write the failing tests**
  - `Mapping_condition_requires_both_value_lists`: Condition is mapped and the Bad list is empty, which gives the error `Map at least one Good value and one Bad value when Condition is mapped.`
  - `A_value_in_both_lists_is_rejected`: `"BS"` in both gives an error that names `BS`.
  - `Lists_are_ignored_and_cleared_when_condition_is_not_mapped`: both are stored as null.
  - `Lists_are_stored_normalised`: `" good ; GS;gs"` is stored as `"good;GS"`.

- [ ] **Step 2: Run them to verify they fail**

- [ ] **Step 3: Implement**

The editor shows the two inputs only while a column is mapped to `Condition`, and pre-fills a new mapping with the Global Constraints defaults.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Commit**

`feat: let a Stock template say which values mean good and bad stock`

---

### Task 6: Analysis — resolve area and condition and refuse templates without Area

**Files:**
- Modify:
  - `GiselX.Service/StockWorkbookAnalysisService.cs` (new constructor dependencies `IStockAreaRepository` and `ICompanyRepository`, the latter for the company name)
  - `GiselX.Service/StockUploadService.cs` (`ToStaging` copies the four new fields)
  - `GiselX.Repository/StockUploadRepository.cs` (`InsertStagingAsync` column list)
- Test: `GiselX.Tests/StockWorkbookAnalysisServiceTests.cs`, `GiselX.Tests/StockUploadServiceTests.cs`

**Interfaces:**
- Consumes: `StockAreaMatcher`, `StockConditionResolver` (Task 3), `IStockAreaRepository.ListAsync(companyId, activeOnly: true, …)` (Task 4), and the template lists (Task 5).
- Produces: analysed `StockStagingRowDto` rows with `StockAreaId` and `Condition` set. A resolution failure appends a `StockStagingErrorDto` with `Field = Area|Condition`, `RejectedValue` = the raw text, `WorksheetName` set, and `IsValid = false`.

- [ ] **Step 1: Write the failing tests**

`StockWorkbookAnalysisServiceTests`:
  - `Rows_get_the_area_and_condition_their_cells_resolve_to`.
  - `An_unresolvable_area_invalidates_only_that_row_and_names_the_text`.
  - `A_template_without_area_or_condition_yields_no_area_and_good`, for a company with **no** areas.
  - `A_company_with_areas_refuses_a_template_that_does_not_map_area`: `analysis.Errors` contains `Template '<name>' does not map an Area column, but <company> has Stock areas registered.` and no rows are returned.
  - `Inactive_areas_do_not_resolve`.

`StockUploadServiceTests`:
  - `Upload_stages_the_area_condition_and_raw_texts_of_each_row`: the fake repository's captured staging rows carry all four values.

- [ ] **Step 2: Run them to verify they fail**

- [ ] **Step 3: Implement**

Load the active areas once per analysis and build one `StockAreaMatcher`. Per match:
- if the company has areas and the mapping lacks an `Area` column, that is the refusal;
- otherwise resolve each row whose mapping maps `Area` / `Condition`.

Resolution runs after `Resolve` (item mapping), so a row can carry both kinds of error.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Commit**

`feat: resolve each Stock row's area and condition before publishing`

---

### Task 7: Publication — replace only the uploaded areas

**Files:**
- Modify: `GiselX.Repository/StockUploadRepository.cs` (`PublishValidRowsAsync`)
- Test: `GiselX.Tests/StockSupersedeSqlTests.cs`

**Interfaces:**
- The signature is unchanged: `PublishValidRowsAsync(long batchId, int companyId, StockUploadBatchStatus status, CancellationToken)`.

- [ ] **Step 1: Write the failing SQL-shape tests (add them to `StockSupersedeSqlTests`)**
  - `The_delete_is_limited_to_the_areas_of_this_uploads_valid_rows`: the delete statement contains `s.StockUploadBatchId=@BatchId AND s.IsValid=1` and `COALESCE(s.StockAreaId,0)=COALESCE(stock.StockAreaId,0)`.
  - `A_prior_batch_is_replaced_only_when_none_of_its_rows_remain`: the `UPDATE prior SET Status=4` statement contains `NOT EXISTS (SELECT 1 FROM Stock remaining WHERE remaining.StockUploadBatchId=prior.Id)`.
  - `Publication_copies_area_and_condition_into_stock`: the `INSERT INTO Stock` column list contains `StockAreaId` and `Condition`.

The existing four tests must keep passing, so both statements stay scoped by company, date, `prior.Id<>@BatchId` and status, and the delete still precedes the update.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~StockSupersedeSqlTests`
Expected: the 3 new tests FAIL.

- [ ] **Step 3: Rewrite the SQL**

Use the spec's delete, written null-safe with `COALESCE(…,0)` (area ids are identity values of at least 1). Then the `NOT EXISTS`-guarded status update, then the insert with the two new columns.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Verify against SQL Server**

These steps run the real upload path through the app with test workbooks. Create the workbooks with NPOI/ClosedXML in the scratchpad, or by hand.

On the dev database, for company X with areas Medan and Binjai, upload in this order:
1. Medan for 2026-11-30, then check `Stock` row counts per area.
2. Binjai for 2026-11-30. Medan's rows are still present.
3. Medan again. Only Medan's rows change, and the first Medan batch is `Status=4` while the Binjai batch stays `2`/`3`.
4. One workbook containing both areas, then Medan only. That batch stays live with its Binjai rows.
5. A company without areas, uploading twice for one date. The first batch is Replaced, exactly as today.

- [ ] **Step 6: Commit**

`feat: replace only the areas a Stock upload contains`

---

### Task 8: Preview — show the areas this upload replaces

**Files:**
- Create: `GiselX.Domain/PublishedStockArea.cs`, `GiselX.Service.Dto/StockAreaReplacementDto.cs`
- Modify:
  - `GiselX.Repository.Interface/IStockUploadRepository.cs`, `GiselX.Repository/StockUploadRepository.cs`
  - `GiselX.Service.Dto/StockUploadPreflightDto.cs`, `GiselX.Service/StockUploadService.cs` (`PreflightAsync`, `ToPreflight`)
  - `GiselX.Web/Views/Stocks/Index.cshtml` (review modal)
- Test: `GiselX.Tests/StockUploadServiceTests.cs`

**Interfaces:**
- Produces:
  - `PublishedStockArea { int? StockAreaId; string? AreaName; int RowCount; DateTime UploadedDate; string UploadedBy; string OriginalFileName; }`.
  - `IStockUploadRepository.GetPublishedAreasAsync(int companyId, DateTime stockDate, CancellationToken) : Task<IReadOnlyList<PublishedStockArea>>`. It returns one entry per area per live batch (status 2/3) for that company and date. The default interface implementation throws `NotSupportedException`, as the existing fakes expect.
  - `StockAreaReplacementDto { string AreaName; int ValidRows; decimal SaldoAkhirGood; decimal SaldoAkhirBad; IReadOnlyList<PublishedStockArea> Replaces; }`. `AreaName` is `"(no area)"` for null.
  - `StockUploadPreflightDto.Areas (IReadOnlyList<StockAreaReplacementDto>)` and `StockUploadPreflightDto.UntouchedAreaNames (IReadOnlyList<string>)`.

- [ ] **Step 1: Write the failing tests**
  - `Preflight_lists_each_uploaded_area_with_good_and_bad_totals`.
  - `Preflight_names_what_each_area_replaces_and_leaves_other_areas_untouched`: the fake repository returns published Medan and Binjai, and the upload carries Medan only. `Areas[0].Replaces` is Medan's entry, and `UntouchedAreaNames` is `["Binjai"]`.
  - `Preflight_for_a_company_without_areas_shows_the_no_area_snapshot_it_replaces`.
  - `Preflight_still_performs_no_writes` (the existing guarantee).

- [ ] **Step 2: Run them to verify they fail**

- [ ] **Step 3: Implement the repository query, the service mapping and the modal section**

The modal section is titled "Areas", with one row per uploaded area that shows its rows, Good and Bad totals, and "replaces …". It also shows a "Not affected" line for the untouched areas.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Verify in the browser**

Preview a Medan workbook after Medan and Binjai are already published, and check that the replacement and "Not affected" lines are correct.

- [ ] **Step 6: Commit**

`feat: say which areas a Stock upload replaces before it is confirmed`

---

### Task 9: Reading — per-area ranking, grid columns, summary tiles, download

**Files:**
- Modify:
  - `GiselX.Repository/StockSnapshotPeriod.cs`
  - `GiselX.Repository/StockRepository.cs` (`SelectAsync`, `SummarizeAsync`, `SelectByBatchAsync`, `GetProjection`, filter allow-lists)
  - `GiselX.Domain/StockSelectionSummary.cs`, `GiselX.Service.Dto/StockSelectionSummaryDto.cs`, `GiselX.Service/StockService.cs`
  - `GiselX.Service.Dto/StockDto.cs` and the Mapperly mapping if it does not pick up the new members automatically
  - `GiselX.Web/Views/Stocks/Index.cshtml` (columns, filters, tiles)
  - `GiselX.Web/Controllers/StocksController.cs` (`DownloadBatch` columns)
- Test: `GiselX.Tests/StockSnapshotPeriodTests.cs`, `GiselX.Tests/SelectionSummaryScopeTests.cs`

**Interfaces:**
- Produces:
  - `RankedBatches` CTE columns are `Id, CompanyId, AreaKey, BatchRank`, where `AreaKey = COALESCE(StockAreaId,0)`.
  - Every reader joins `rb.Id=s.StockUploadBatchId AND rb.AreaKey=COALESCE(s.StockAreaId,0) AND rb.BatchRank=1`.
  - The projection adds `s.StockAreaId, a.Name AS AreaName, s.Condition` with `LEFT JOIN StockArea a ON a.Id=s.StockAreaId`.
  - Filters accept `AreaName` (`a.Name LIKE`) and `Condition` (`Good`/`Bad` matched to 0/1).
  - `StockSelectionSummary` and its DTO gain `SaldoAkhirGood` and `SaldoAkhirBad`.

- [ ] **Step 1: Write the failing tests**

Update `StockSnapshotPeriodTests`:
  - `A_chosen_month_ranks_each_area_of_each_distributor_latest_first`: SQL contains `PARTITION BY b.CompanyId, COALESCE(d.StockAreaId,0) ORDER BY b.StockDate DESC, b.CompletedDate DESC, b.Id DESC`, and still `Status IN (2,3)` and the period window.
  - `Without_a_month_the_latest_published_batch_per_area_is_kept`.
  - `Grid_count_and_summary_join_on_the_area_key`: each of `SelectAsync` and `SummarizeAsync` in `StockRepository.cs` contains `rb.AreaKey=COALESCE(s.StockAreaId,0)`.

Add to `SelectionSummaryScopeTests`:
  - `Stock_summary_splits_saldo_akhir_by_condition`: `SummarizeAsync` contains `CASE WHEN s.Condition=0` and `CASE WHEN s.Condition=1`.

- [ ] **Step 2: Run them to verify they fail**

- [ ] **Step 3: Implement**

The CTE ranks `(batch, area)` pairs taken from `SELECT DISTINCT StockUploadBatchId, StockAreaId FROM Stock`, joined to live batches, so a partly replaced batch ranks only the areas it still holds.

Grid:
- adds the Area and Condition columns after Distributor, with a text filter for Area and a Good/Bad select for Condition in the footer;
- shows the footer to Distributors too;
- the Distributor filter input stays Administrator-only.

Two tiles are added, "Saldo Akhir Good" and "Saldo Akhir Bad". `DownloadBatch` gains "Area" and "Condition" headers after "Product Name".

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Verify against SQL Server and in the browser**

With Task 7's data:
- **November 2026:** Medan and Binjai both show at their own latest dates, the filters work, the tiles split Good/Bad, and the tiles equal the sum of the grid. Check this with the temporary repository-against-database comparison used for the summary work, then delete that test.
- **Distributor login:** sees only their own company's areas.
- **`DownloadBatch`:** the export carries Area and Condition.

- [ ] **Step 6: Commit**

`feat: show the latest Stock snapshot of every area, with condition`

---

### Task 10: Reporting view, and docs

**Files:**
- Modify:
  - `GiselX.Web/DatabaseScripts/Reporting_StockAndSellout.sql` (`vw_GiselStock_Live` and any proc reading it)
  - `CLAUDE.md` (the supersession paragraph)
- Test: `GiselX.Tests/ReportingStockViewScriptTests.cs`

- [ ] **Step 1: Write the failing tests**

`ReportingStockViewScriptTests` reads the script. It asserts the live rank `PARTITION BY b.CompanyId, b.StockDate, COALESCE(d.StockAreaId,0)`, the output columns `AreaName` and `Condition`, and `CASE stock.Condition WHEN 1 THEN 'Bad' ELSE 'Good' END`.

- [ ] **Step 2: Run them to verify they fail**

- [ ] **Step 3: Update the view**

Rank `(batch, area)` pairs exactly as Task 9 does, and add `LEFT JOIN dbo.StockArea area` with `area.Name AS AreaName`.

Update `CLAUDE.md`: Stock supersession replaces the uploaded **areas** of a snapshot date, and every Stock reader ranks per `(CompanyId, area)`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Verify against SQL Server**

Run the script with `sqlcmd … -i Reporting_StockAndSellout.sql` twice. Then check that `SELECT … FROM vw_GiselStock_Live` filtered to the dates the Stock page shows per area matches the page's row count and Saldo Akhir for November 2026.

- [ ] **Step 6: Commit**

`feat: report Stock by area and condition`
