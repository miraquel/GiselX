# Copy Mappings and Unmapped Item Follow-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the admin copy Sellout/Stock template mappings and item mappings from one company to another, map unmapped items in bulk, and see all companies with unmapped items on one page.

**Architecture:** A new `MappingCopyService` evaluates source templates against the target using the *same* validation the save paths use (made `internal static` and shared), then inserts through the existing repositories in the ambient transaction with one commit. Bulk mapping is a new `ItemMappingService.SaveBulkAsync` behind a grid on the existing Unmapped tab. The overview is a cross-company aggregate of the existing unmapped-item query. No schema change.

**Tech Stack:** ASP.NET Core MVC (net10.0), Dapper inline SQL, xUnit with hand-rolled fakes.

**Spec:** `docs/superpowers/specs/2026-10-26-copy-mappings-and-unmapped-items-design.md`

## Global Constraints

- No migration, no schema change, no manual SQL script.
- Admin screens in English, matching the existing mapping screens.
- Services call `transaction.Commit()` exactly once on success; never on a refused request.
- Copy only adds: never update or delete an existing target row.
- Every new SQL is scoped by explicit parameters and passes the ambient `IDbTransaction`.
- Test naming `Subject_verb_expected_outcome`, snake case after the first word.

## Review Focus

1. **Source and target are the same company** → refused with 400, nothing written (test in Task 2).
2. **Source template already invalid by today's rules** (legacy template missing a now-required field) → `Invalid` with the validator's messages, never inserted (Task 2).
3. **Inactive source template ticked whose format the target reads actively** → copied inactive, not skipped (Task 2).
4. **Bulk save with two rows for the same distributor id differing only by case/padding** → second row gets the duplicate error, first saves (Task 4).
5. **Bulk save where every filled row is invalid** → no commit, all typed values returned with errors (Task 4).

---

### Task 1: Share template validation

**Files:**
- Modify: `GiselX.Service/StockTemplateMappingService.cs`
- Modify: `GiselX.Service/DistributorTemplateMappingService.cs`
- Test: `GiselX.Tests/StockTemplateMappingServiceTests.cs` (existing tests must stay green)

**Interfaces:**
- Produces:
  - `internal static List<string> StockTemplateMappingService.Validate(SaveStockTemplateMappingDto dto, IReadOnlyList<StockTemplateMapping> existingMappings)` (already exists, change `private` → `internal`)
  - `internal static string? StockTemplateMappingService.AreaRequirementError(IEnumerable<StockTemplateColumnDto> columns, IReadOnlyList<StockArea> areas)` — returns the exact message `SaveAsync` builds today when no column is `StockCanonicalField.Area` and `areas.Count > 0`, else null. `SaveAsync` calls it instead of its inline block.
  - `internal static List<string> DistributorTemplateMappingService.Validate(SaveDistributorTemplateMappingDto dto, IReadOnlyList<DistributorTemplateMapping> existingMappings)` (`private` → `internal`)

- [ ] **Step 1:** Make the changes above; behaviour of `SaveAsync` unchanged.
- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~TemplateMappingService"` → all PASS.
- [ ] **Step 3:** Commit `refactor: share template mapping validation with other services`.

### Task 2: MappingCopyService

**Files:**
- Create: `GiselX.Service.Dto/MappingCopyDtos.cs`
- Create: `GiselX.Service.Interface/IMappingCopyService.cs`
- Create: `GiselX.Service/MappingCopyService.cs`
- Modify: `GiselX.Service/ServiceCollectionExtensions.cs` (register scoped)
- Modify: `GiselX.Repository/CompanyRepository.cs` — `GetAllAsync` selects `Id, Name, Location`
- Test: `GiselX.Tests/MappingCopyServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 validators; `IDistributorTemplateMappingRepository`, `IStockTemplateMappingRepository`, `IItemMappingRepository` (`GetByCompanyAsync`, `InsertAsync`), `IStockAreaRepository.ListAsync(companyId, false, ct)`, `ICompanyRepository.GetAllAsync` / `GetCompanyByIdAsync`, `IDbTransaction`.
- Produces (DTOs in `GiselX.Service.Dto`):
  ```csharp
  public enum TemplateCopyOutcome { WillCopy, SkippedNameExists, SkippedFormatExists, Invalid }
  public sealed class CompanyOptionDto { int Id; string DisplayName; }          // "Name – Location", location omitted when blank
  public sealed class TemplateCopyCandidateDto { int SourceId; string Name; string WorksheetName;
      int HeaderRowNumber; int FirstDataRowNumber; int ColumnCount; bool IsActive;
      TemplateCopyOutcome Outcome; List<string> Reasons; }
  public sealed class MappingCopyPreviewDto { int SourceCompanyId; int TargetCompanyId;
      List<TemplateCopyCandidateDto> SelloutTemplates; List<TemplateCopyCandidateDto> StockTemplates;
      int ItemMappingsToCopy; int ItemMappingsSkipped; bool TargetItemMappingEnabled; }
  public sealed class MappingCopyRequestDto { int SourceCompanyId; int TargetCompanyId;
      List<int> SelloutMappingIds; List<int> StockMappingIds; bool CopyItemMappings; }
  public sealed class MappingCopyResultDto { int SelloutCopied; int StockCopied;
      int ItemMappingsCopied; int ItemMappingsSkipped; List<string> Skipped; }   // "Sellout 'Name': reason"
  ```
  (properties with `{ get; set; }`, lists initialised to `[]`.)
- `IMappingCopyService`:
  - `Task<IReadOnlyList<CompanyOptionDto>> ListSourceCompaniesAsync(int targetCompanyId, CancellationToken ct)` — all companies except the target, ordered by display name.
  - `Task<ServiceResponse<MappingCopyPreviewDto>> PreviewAsync(int sourceCompanyId, int targetCompanyId, CancellationToken ct)`
  - `Task<ServiceResponse<MappingCopyResultDto>> CopyAsync(MappingCopyRequestDto request, string username, CancellationToken ct)`

**Evaluation rule (one private function used by both Preview and Copy):** walk the candidate templates ordered by name. For each, build the `Save*Dto` from the source entity (`Id = null`, `CompanyId = target`) and decide, first match wins:
1. name equals (trimmed, OrdinalIgnoreCase) an existing target template or an earlier `WillCopy` candidate → `SkippedNameExists`, reason `"A mapping with this name already exists for the company."`
2. candidate `IsActive` and its format key equals that of an active target template or an earlier active `WillCopy` candidate → `SkippedFormatExists`, reason `"This company already has an active template for this format."` Format key: Sellout = `HeaderSignature` (ordinal); Stock = `(WorksheetName` OrdinalIgnoreCase, `HeaderSignature` ordinal`)`.
3. `Validate(...)` errors, filtered to drop the duplicate-name message (handled by rule 1), plus for Stock `AreaRequirementError(...)` against the **target's** areas → `Invalid` with those reasons.
4. otherwise `WillCopy`.
Preview evaluates all source templates; Copy evaluates only the requested ids (ids not belonging to the source are ignored).

Item mappings: source mappings whose trimmed DistributorItemId (OrdinalIgnoreCase) is not on the target are copied with `IsActive` preserved, `CreatedBy = username`, `CreatedDate = DateTime.UtcNow`, `Modified*` null. Templates copy Name/Worksheet/rows/signature/columns/Good/BadConditionValues verbatim with source `IsActive`, `CreatedBy = username`.

Refusals (no commit, nothing written): source == target → 400 `"Choose a different company to copy from."`; either company missing → 404 `"The company was not found."`. Copy with nothing to write → success, no commit, message `"Nothing was copied."`. Otherwise one `Commit()` and message `"Mappings copied."`.

- [ ] **Step 1: Write failing tests** in `MappingCopyServiceTests` (fakes per repository, modelled on `StockTemplateMappingServiceTests`):
  - `PreviewAsync_refuses_copying_a_company_onto_itself` → `StatusCode == 400`.
  - `PreviewAsync_marks_a_template_whose_name_exists_on_the_target_as_skipped` → `Outcome == SkippedNameExists`.
  - `PreviewAsync_marks_an_active_sellout_template_with_a_signature_already_active_on_the_target_as_skipped` → `SkippedFormatExists`.
  - `PreviewAsync_does_not_skip_an_inactive_template_for_a_competing_format` → `WillCopy`.
  - `PreviewAsync_compares_stock_formats_per_worksheet` — same signature, different worksheet → `WillCopy`.
  - `PreviewAsync_refuses_a_stock_template_without_area_when_the_target_has_areas` → `Invalid`, reason contains `"must map an Area column"`.
  - `PreviewAsync_reports_a_legacy_template_missing_a_required_field_as_invalid` → `Invalid`, reason starts `"Required canonical field"`.
  - `PreviewAsync_counts_item_mappings_to_copy_and_skip_case_insensitively` — source `" d-1 "`, target `"D-1"` → skipped 1.
  - `CopyAsync_inserts_selected_templates_for_the_target_and_commits_once` — inserted `CompanyId == target`, `CreatedBy == "admin"`, columns and condition values equal source, `IsActive` equals source, commit called once.
  - `CopyAsync_does_not_let_two_selected_active_copies_of_one_format_both_land` → one inserted, `Skipped` has one entry.
  - `CopyAsync_copies_item_mappings_with_their_source_status_and_skips_existing_ones`.
  - `CopyAsync_ignores_ids_that_do_not_belong_to_the_source_company`.
  - `CopyAsync_with_nothing_copyable_does_not_commit`.
  - `ListSourceCompaniesAsync_excludes_the_target_and_appends_location` → `"PT ABC – Medan"`, and `"PT XYZ"` when location blank.
- [ ] **Step 2:** `dotnet test --filter FullyQualifiedName~MappingCopyServiceTests` → FAIL (types missing).
- [ ] **Step 3:** Implement DTOs, interface, service, registration, `GetAllAsync` column change.
- [ ] **Step 4:** Same filter → PASS; then full `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: copy template and item mappings between companies`.

### Task 3: Copy screen

**Files:**
- Create: `GiselX.Web/Controllers/MappingCopyController.cs` (`[Area("Admin")]`, `[Authorize(PermissionConstants.Companies.Edit)]`)
- Create: `GiselX.Web/Models/MappingCopyViewModel.cs`
- Create: `GiselX.Web/Areas/Admin/Views/MappingCopy/Index.cshtml`, `Result.cshtml`
- Modify: `GiselX.Web/Areas/Admin/Views/Companies/Edit.cshtml` — button "Copy Mappings from Another Company" → `MappingCopy/Index?companyId=`
- Test: `GiselX.Tests/MappingCopyControllerTests.cs`

**Interfaces:**
- Consumes: `IMappingCopyService` (Task 2), `IAuthorizationService` for the three `*.Manage` policies.
- Produces: `MappingCopyViewModel { int TargetCompanyId; string TargetCompanyName; int? SourceCompanyId; IReadOnlyList<CompanyOptionDto> Sources; MappingCopyPreviewDto? Preview; bool CanCopySellout; bool CanCopyStock; bool CanCopyItems; }`
- Actions: `GET Index(int companyId, int? sourceCompanyId, CancellationToken)`; `POST Copy(int companyId, int sourceCompanyId, List<int>? selloutMappingIds, List<int>? stockMappingIds, bool copyItemMappings, CancellationToken)` with `[ValidateAntiForgeryToken]` → `Result` view with `MappingCopyResultDto`, or back to Index with `TempData["ErrorMessage"]` on failure.

Index: a GET form with the source dropdown (auto-submit on change); when a source is chosen, a POST form with one table per permitted section (checkbox per `WillCopy` row, checked when `IsActive`; disabled checkbox and outcome badge + reasons otherwise), the item-mapping checkbox with both counts, and the warning `"Item mapping is disabled for this company; copied item mappings take effect once it is enabled on the company screen."` when `!TargetItemMappingEnabled`. Result: counts per type, the `Skipped` list, link back to Company Edit.

- [ ] **Step 1: Failing tests:**
  - `Copy_drops_sections_the_user_may_not_manage` — user authorised only for `StockTemplateMappings.Manage`; service receives empty `SelloutMappingIds` and `CopyItemMappings == false`.
  - `Index_without_source_lists_sources_and_has_no_preview`.
- [ ] **Step 2:** run → FAIL. **Step 3:** implement. **Step 4:** run → PASS.
- [ ] **Step 5:** Commit `feat: add copy mappings screen to company admin`.

### Task 4: Bulk mapping on the Unmapped tab

**Files:**
- Create: `GiselX.Domain/GmkItemSuggestion.cs` (`GmkItemId`, `GmkItemName`)
- Modify: `GiselX.Repository.Interface/IItemMappingRepository.cs`, `GiselX.Repository/ItemMappingRepository.cs` — `Task<IReadOnlyList<GmkItemSuggestion>> GetGmkItemSuggestionsAsync(CancellationToken)`: distinct trimmed `GmkItemId` over all companies' **active** mappings, `MAX` trimmed name per id, ordered by id.
- Modify: `GiselX.Service.Dto` — `BulkItemMappingResultDto { int SavedCount; List<BulkItemMappingRowErrorDto> RowErrors; }`, `BulkItemMappingRowErrorDto { string DistributorItemId; List<string> Errors; }`, `GmkItemSuggestionDto`.
- Modify: `IItemMappingService` / `ItemMappingService`:
  - `Task<ServiceResponse<BulkItemMappingResultDto>> SaveBulkAsync(int companyId, IReadOnlyList<SaveItemMappingDto> rows, string username, CancellationToken)` — ignore rows whose GmkItemId and GmkItemName are both blank; validate each remaining row with the existing `Validate` against existing mappings **plus rows accepted earlier in this call**; insert valid rows as active (`ItemMappingDuplicateException` → row error); commit once if `SavedCount > 0`. Errors go in `RowErrors`, not `response.Errors`. Message when saved: `"{n} item mapping(s) saved. Uploads that contained these items were published without those rows; ask the distributor to upload them again."`
  - `Task<IReadOnlyList<GmkItemSuggestionDto>> ListGmkItemSuggestionsAsync(CancellationToken)`
- Modify: `GiselX.Web/Controllers/ItemMappingsController.cs` — extract Index model building into a private method; `Index` gains `string? tab`; new `POST SaveUnmapped(int companyId, List<UnmappedItemMappingInput> rows)` `[Authorize(ItemMappings.Manage)]`: on any row error re-render Index (tab `unmapped`) with typed values and errors; otherwise redirect to Index with `TempData["StatusMessage"]`.
- Create: `GiselX.Web/Models/UnmappedItemMappingInput.cs` (`DistributorItemId`, `DistributorItemName`, `GmkItemId`, `GmkItemName`, `List<string> Errors`)
- Modify: `ItemMappingListViewModel` — `string? ActiveTab`, `IReadOnlyList<GmkItemSuggestionDto> GmkItems`, `IReadOnlyDictionary<string, UnmappedItemMappingInput> UnmappedInputs` (key: distributor id upper-trimmed), `bool CanManage`.
- Modify: `GiselX.Web/Areas/Admin/Views/ItemMappings/Index.cshtml` — when `CanManage`, the Unmapped table is inside a POST form with inputs per row (GMK Item ID with `list="gmkItems"`, GMK Item Name, editable Distributor Item Name), a `<datalist id="gmkItems">`, a small script filling the name when a known id is picked, and a "Save mappings" button; row errors under the row. Activates the Unmapped tab when `ActiveTab == "unmapped"`. Keep the per-row Map button.
- Test: `GiselX.Tests/ItemMappingServiceTests.cs`, `GiselX.Tests/ItemMappingControllerTests.cs`, `GiselX.Tests/UnmappedDistributorItemQueryTests.cs`

- [ ] **Step 1: Failing tests:**
  - `SaveBulkAsync_ignores_rows_with_no_gmk_values`
  - `SaveBulkAsync_saves_valid_rows_and_reports_invalid_ones_then_commits_once`
  - `SaveBulkAsync_rejects_a_second_row_for_the_same_distributor_id_ignoring_case_and_padding`
  - `SaveBulkAsync_with_only_invalid_rows_does_not_commit`
  - `Gmk_suggestion_query_reads_only_active_mappings_across_companies` (SQL shape: contains `WHERE IsActive = 1`, `GROUP BY LTRIM(RTRIM(GmkItemId))`, no `@CompanyId`)
  - `SaveUnmapped_rerenders_unmapped_tab_with_typed_values_when_a_row_fails`
- [ ] **Step 2:** run → FAIL. **Step 3:** implement. **Step 4:** run → PASS, full suite PASS.
- [ ] **Step 5:** Commit `feat: map unmapped items in bulk from the unmapped tab`.

### Task 5: Unmapped items overview

**Files:**
- Create: `GiselX.Domain/UnmappedItemCompanySummary.cs` (`CompanyId`, `CompanyName`, `CompanyLocation`, `UnmappedItemCount`, `OccurrenceCount`, `LastSeenDate`)
- Modify: `IItemMappingRepository` / `ItemMappingRepository` — `Task<IReadOnlyList<UnmappedItemCompanySummary>> GetUnmappedSummaryByCompanyAsync(CancellationToken)`. Same `Discovered` sources, trimming and `ItemMappingId IS NULL` filter as `GetUnmappedDistributorItemsAsync`, but carrying `batch.CompanyId`, joined to `Company` with `ItemMappingEnabled = 1`, `NOT EXISTS` matching `mapping.CompanyId = discovered.CompanyId`; aggregate per (company, normalized id) then per company; order `LastSeenDate DESC`.
- Modify: `IItemMappingService` / `ItemMappingService` — `Task<ServiceResponse<IReadOnlyList<UnmappedItemCompanySummaryDto>>> ListUnmappedSummaryAsync(CancellationToken)` (+ DTO in `GiselX.Service.Dto`).
- Create: `GiselX.Web/Controllers/UnmappedItemsController.cs` (`[Area("Admin")]`, `Index` with `[Authorize(ItemMappings.Index)]`), `GiselX.Web/Areas/Admin/Views/UnmappedItems/Index.cshtml` — table Company, Location, Unmapped items, Rows, Last seen; each row links to `ItemMappings/Index?companyId=…&tab=unmapped`; empty state `"Every company with item mapping enabled is fully mapped."`
- Modify: `GiselX.Web/Views/Shared/Sections/Menu/_VerticalMenu.cshtml` — "Unmapped Items" entry after Companies, gated on `ItemMappings.Index`, icon `ri-error-warning-line`.
- Test: `GiselX.Tests/UnmappedDistributorItemQueryTests.cs`

- [ ] **Step 1: Failing tests:**
  - `Unmapped_summary_query_only_covers_companies_with_item_mapping_enabled` (contains `ItemMappingEnabled = 1`)
  - `Unmapped_summary_query_matches_existing_mappings_within_the_same_company` (contains `mapping.CompanyId = discovered.CompanyId` and `UPPER(LTRIM(RTRIM(mapping.DistributorItemId)))`)
  - `Unmapped_summary_query_reads_both_staging_sources_with_the_unmapped_filter` (two `staging.ItemMappingId IS NULL`)
- [ ] **Step 2:** run → FAIL. **Step 3:** implement. **Step 4:** run → PASS, full suite PASS.
- [ ] **Step 5:** Commit `feat: list companies with unmapped items on one page`.

### Task 6: Verify against SQL Server

- [ ] `dotnet build` clean, `dotnet test` all PASS.
- [ ] Run the app against the dev database and exercise: copy between two companies (inspect the inserted `DistributorTemplateMapping`, `StockTemplateMapping`, `ItemMapping` rows), re-run the same copy (everything skipped, nothing written), bulk-map unmapped items, confirm the overview count and the tab count drop together. Record what was run in the commit/PR text, since the suite never executes the SQL.
