# Sellout Month View and Date-Range Replacement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Sellout month view shows every published row of the month, and a Sellout upload replaces only the invoice-date range of its valid rows.

**Architecture:**
- Remove the `CreatedDate = MAX(CreatedDate)` filter from both stored procedures and the inline Sellout readers. One migration ships it, together with an index swap that allows two live batches per week.
- Rewrite `PromoteGroupAsync` so its delete is scoped by date range instead of by whole ISO week.
- Replace the preview's partial-week warnings with a Replacement section fed by a new repository read.

**Tech Stack:** .NET 10, ASP.NET Core MVC, Dapper, EF Core migrations (schema authority), SQL Server stored procedures, xUnit with hand-rolled fakes.

**Spec:** `docs/superpowers/specs/2026-10-22-sellout-month-view-and-date-range-replacement-design.md`

## Global Constraints

- **Sellout batch status.** `SelloutUploadBatchStatus` is `2` Completed, `3` Replaced, `4` CompletedWithWarnings. Live means `2` or `4`. These values are not the Stock enum's.
- **Replacement range.**
  - `From` and `To` are the earliest and latest `InvoiceDate` date of the group's **valid** staging rows.
  - The delete predicate is `sales.CompanyId=@CompanyId AND sales.InvoiceDate >= @From AND sales.InvoiceDate < DATEADD(day,1,@To)`.
  - It covers every row of the company, including rows without a batch.
- **Replaced marking.** A prior batch is marked Replaced only if it was live, had `SalesTransaction` rows in the range before the delete, and has none after. Only those batches lose their staging rows.
- **Concurrency.** `PromoteGroupAsync` takes `sp_getapplock` on resource `GiselX-SelloutPublish-<CompanyId>`: `Exclusive`, owner `Transaction`, timeout 30000 ms. It `THROW`s 50003 when the result is below 0.
- **Index.**
  - Drop the filtered unique index `IX_SelloutUploadBatch_CompanyId_ReportingWeekStart` (`WHERE [Status] = 2`).
  - Recreate it with the same name as a non-unique index on `(CompanyId, ReportingWeekStart)` with no filter.
- **Preview copy (verbatim):**
  - `Confirming replaces your published sales from {From:dd MMM yyyy} to {To:dd MMM yyyy}.`
  - `Nothing is published for these dates yet.`
- **Migration and script naming.** Migration name `SelloutDateRangeReplacement`. The companion script is `GiselX.Web/DatabaseScripts/<id>_SelloutDateRangeReplacement.sql` and follows CLAUDE.md:
  - `XACT_ABORT`, an explicit transaction, `sp_getapplock`, guards and the history insert;
  - procedure bodies through `sp_executesql`.
- **Stored procedure sources.** Procedure bodies in the migration and the script are byte-for-byte the `GiselX.Web/StoredProcedures/*.sql` source, compared whitespace-normalised.
- **Verification databases.** Scratch databases only (`GiselX_SelloutCheck_*` on localhost, Windows auth). Never `GiselXDb`.

## Review Focus

1. **A file whose valid rows span two months** (for example 28 Sep–7 Oct). The range must be one continuous span across both months, deleting nothing before 28 Sep and nothing after 7 Oct. Pinned in Task 2's scratch-DB scenario 5.
2. **A batch whose rows are all inside the new range but belong to a different, earlier upload of the same week.** It must become Replaced, and its staging rows must go. A batch that keeps some rows must stay live. Pinned in Task 2, scenario 2.
3. **Two live batches for the same week** must insert without violating the old filtered unique index. Pinned in Task 2, scenario 4.
4. **The month view after the filter is removed.** Grid, count, tiles and Excel download must total the same rows for both Administrator and Distributor. Pinned in Task 1's scratch-DB check.
5. **A refused or errored preview** must not query published ranges, and must still write nothing. Pinned in Task 3.

---

### Task 1: Month view shows every row; migration with the index swap

**Files:**
- Modify:
  - `GiselX.Web/StoredProcedures/GiselSales_Select.sql` (remove the `[CreatedDate] = (SELECT MAX …)` predicate from both result sets)
  - `GiselX.Web/StoredProcedures/GiselSales_SelectByCustPeriod.sql` (remove the same predicate)
  - `GiselX.Repository/SalesTransactionRepository.cs` (`SelectAllAsync`, `SelectAllByPeriodAsync`: drop the `LatestUploads` CTE's `MAX(...) OVER` / `WHERE st.CreatedDate=st.LatestCreatedDate`)
  - `GiselX.Repository/SalesSelectionSummaryQuery.cs` (same; keep the company, period and filter rules)
  - `GiselX.Web/Context/GiselXDbContext.cs` (`SelloutUploadBatch`: `HasIndex(CompanyId, ReportingWeekStart)` without `IsUnique`/`HasFilter`)
- Create (generated): `GiselX.Web/Context/Migrations/<id>_SelloutDateRangeReplacement.cs` (+ Designer, snapshot)
- Create: `GiselX.Web/DatabaseScripts/<id>_SelloutDateRangeReplacement.sql`
- Test:
  - `GiselX.Tests/SelloutMonthViewTests.cs` (new)
  - `GiselX.Tests/SelectionSummaryScopeTests.cs` (invert the latest-upload test)
  - `GiselX.Tests/SelloutPersistenceModelTests.cs`
  - `GiselX.Tests/SelloutDateRangeManualSqlScriptTests.cs` (new)

**Interfaces:**
- Produces: no signature changes. The readers simply return more rows.

- [ ] **Step 1: Write the failing tests**

`SelloutMonthViewTests`:
- `Distributor_grid_procedure_keeps_every_row_of_the_month`: `GiselSales_Select.sql` does not contain `MAX([CreatedDate])`.
- `Download_procedure_keeps_every_row_of_the_month`: `GiselSales_SelectByCustPeriod.sql` does not contain `MAX([CreatedDate])`.
- `Administrator_grid_and_download_keep_every_row_of_the_month`: the `SelectAllAsync` and `SelectAllByPeriodAsync` bodies in `SalesTransactionRepository.cs` contain neither `LatestCreatedDate` nor `MAX(st.CreatedDate)`.
- `Migration_installs_both_procedures_from_their_readable_source`: the migration file named `*_SelloutDateRangeReplacement.cs` contains the whitespace-normalised source of both `.sql` files. Reuse the normalisation approach of `StoredProcedureDeploymentTests`.

`SelectionSummaryScopeTests`: replace `Sellout_summary_keeps_only_each_companys_latest_upload_in_the_period_like_the_grid` with `Sellout_summary_keeps_every_row_of_the_period_like_the_grid`. It asserts that the SQL does **not** contain `LatestCreatedDate` and still contains `YEAR(st.InvoiceDate)=@Year AND MONTH(st.InvoiceDate)=@Month`.

`SelloutPersistenceModelTests`: add `Batches_of_one_week_are_indexed_but_not_unique`. The index on `(CompanyId, ReportingWeekStart)` exists, `IsUnique` is false, and `GetFilter()` is null.

`SelloutDateRangeManualSqlScriptTests` asserts, on `DatabaseScripts/*_SelloutDateRangeReplacement.sql`:
- it contains `SET XACT_ABORT ON`, `BEGIN TRANSACTION`, `sp_getapplock` and `sp_executesql`;
- it contains both procedure names with `CREATE OR ALTER PROCEDURE`;
- it contains a guarded `DROP INDEX IX_SelloutUploadBatch_CompanyId_ReportingWeekStart` and a `CREATE INDEX IX_SelloutUploadBatch_CompanyId_ReportingWeekStart` that has no `UNIQUE` and no `WHERE`;
- it contains `INSERT INTO dbo.__EFMigrationsHistory` with the migration id;
- it contains no `DELETE FROM`, `TRUNCATE` or `DROP TABLE`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SelloutMonthViewTests|FullyQualifiedName~SelectionSummaryScopeTests|FullyQualifiedName~SelloutPersistenceModelTests|FullyQualifiedName~SelloutDateRangeManualSqlScriptTests"`
Expected: the new and changed tests FAIL.

- [ ] **Step 3: Implement**

1. Edit the two `.sql` sources and the three C# queries.
2. Change the index configuration.
3. Generate the migration with `dotnet ef migrations add SelloutDateRangeReplacement --project GiselX.Web`.
4. In `Up`, add `migrationBuilder.Sql` with the new `CREATE OR ALTER` bodies.
5. In `Down`, restore the previous bodies. Take them from `20260911023244_WidenQuantityPrecision.cs` for `GiselSales_Select` and from `20260811180000_AddCompanyItemIdentityMapping.cs` for `GiselSales_SelectByCustPeriod`, as last deployed.

If `migrations add` emits a full-schema migration (some earlier migrations have no Designer), do not keep it. Write the migration, its Designer and the snapshot change by hand, and confirm with `dotnet ef migrations has-pending-model-changes --project GiselX.Web`.

Then write the manual script.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass, `StoredProcedureDeploymentTests` included.

- [ ] **Step 5: Verify against SQL Server**

1. Build `GiselX_SelloutCheck_T1` with `dotnet ef database update --project GiselX.Web --connection "Server=localhost;Database=GiselX_SelloutCheck_T1;Trusted_Connection=True;TrustServerCertificate=true;"`.
2. Seed by SQL one company with four weekly groups and batches in October 2026, each with its own `CreatedDate`, plus a second company.
3. With a temporary xUnit test (deleted before committing), call the real `SalesTransactionRepository.SelectAsync` (Distributor proc path and Administrator path), `SelectByCustPeriodAsync` and `SummarizeAsync`.
4. Assert:
   - all four weeks are present;
   - grid count equals the summary row count and the download row count;
   - totals equal `SELECT … FROM vw_GiselSales_Live`, after running `Reporting_StockAndSellout.sql` on the scratch database.
5. Separately, build a database up to the previous migration, run the manual script twice, and compare procedures (`OBJECT_DEFINITION`) and indexes with the EF-built database.
6. Drop the scratch databases.

- [ ] **Step 6: Commit**

`fix: show every published Sellout row of the chosen month`

---

### Task 2: Publication replaces only the invoice-date range of the valid rows

**Files:**
- Modify: `GiselX.Repository/SelloutUploadRepository.cs` (`PromoteGroupAsync`), `CLAUDE.md` (the Sellout supersession sentences)
- Test: `GiselX.Tests/SelloutReplacementSqlTests.cs` (new)

**Interfaces:**
- Signature unchanged: `PromoteGroupAsync(long groupId, CancellationToken)`.

- [ ] **Step 1: Write the failing SQL-shape tests**

Each test slices the relevant statement out of `PromoteGroupAsync`'s SQL rather than matching the whole file.
- `Publication_serialises_per_company_with_a_transaction_applock`: the SQL contains `sp_getapplock`, `GiselX-SelloutPublish-`, `@LockOwner = N'Transaction'` and `THROW 50003`, and the applock comes before the first `DELETE`.
- `The_range_comes_from_the_groups_valid_rows`: the SQL contains `MIN(` and `MAX(` over the `InvoiceDate` of `SelloutStaging` joined to the group's batches, with `IsValid=1`.
- `The_delete_is_limited_to_the_date_range`: the `DELETE sales FROM SalesTransaction` statement contains `sales.InvoiceDate >= @From` and `sales.InvoiceDate < DATEADD(day,1,@To)`, and no longer contains `ReportingWeekStart=currentBatch.ReportingWeekStart`.
- `Only_touched_batches_that_lost_every_row_become_replaced`: the status update to `3` is restricted to batches recorded as touched before the delete, for example a table variable `@Touched` filled with `SELECT DISTINCT SelloutUploadBatchId` of rows in range, and to batches with `NOT EXISTS (SELECT 1 FROM SalesTransaction … WHERE … SelloutUploadBatchId=prior.Id)`.
- `Staging_rows_are_deleted_only_for_batches_that_became_replaced`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~SelloutReplacementSqlTests`
Expected: FAIL.

- [ ] **Step 3: Rewrite `PromoteGroupAsync`'s SQL in this order**

1. Read `@CompanyId` (existing).
2. Take the applock.
3. Compute `@From` / `@To`. Do not proceed to delete if they are NULL.
4. Record the touched live batches.
5. Delete the range.
6. Mark replaced batches.
7. Delete their staging rows.
8. Insert the new valid rows (existing).
9. Update the new batches and the group (existing).

- [ ] **Step 4: Update `CLAUDE.md`**

In "Upload pipelines", a Sellout upload now replaces the invoice-date range of its valid rows, not whole ISO weeks. Several batches can be live for one week, and the Sellout month view shows every live row.

- [ ] **Step 5: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 6: Verify against SQL Server**

Build `GiselX_SelloutCheck_T2` from migrations. With a temporary xUnit test (deleted before committing), seed groups, batches and staging rows by SQL and call the real `PromoteGroupAsync`:
1. September published through 30 Sep, then a group for 1–7 Oct: 28–30 Sep survive and 1–7 Oct is published.
2. Then 1–14 Oct:
   - 1–7 Oct is replaced, not doubled;
   - the first October batch for week 28 Sep–4 Oct is Replaced (status 3) and its staging rows are gone;
   - the September batch for that week stays 2 or 4 with its rows.
3. A full Monday–Sunday re-upload of one week replaces it exactly as before.
4. Two batches live for the same week insert without error.
5. One group spanning 28 Sep–7 Oct deletes nothing before 28 Sep and nothing after 7 Oct.
6. A group whose rows are all invalid for some dates does not widen the range.

Drop the database.

- [ ] **Step 7: Commit**

`fix: replace only the dates a Sellout upload contains`

---

### Task 3: Preview shows the date range it replaces

**Files:**
- Create: `GiselX.Domain/PublishedSelloutBatch.cs`, `GiselX.Service.Dto/SelloutReplacementDto.cs`
- Modify:
  - `GiselX.Repository.Interface/ISelloutUploadRepository.cs`, `GiselX.Repository/SelloutUploadRepository.cs`
  - `GiselX.Service.Dto/SelloutUploadPreflightDto.cs` (add `Replacement`; remove `Warnings`)
  - `GiselX.Service/SelloutUploadService.cs` (remove `PartialWeekWarnings` and `Describe`; build `Replacement` in `PreflightAsync`)
  - `GiselX.Web/Views/SalesTransactions/Index.cshtml` (replace the warnings block with a Replacement section)
- Delete: `GiselX.Tests/SelloutPartialWeekWarningTests.cs`
- Test: `GiselX.Tests/SelloutUploadServiceTests.cs`

**Interfaces:**
- Produces:
  - `PublishedSelloutBatch { long BatchId; DateTime ReportingWeekStart; int RowCount; DateTime UploadedDate; string UploadedBy; string OriginalFileName; }`.
  - `ISelloutUploadRepository.GetPublishedInRangeAsync(int companyId, DateTime from, DateTime to, CancellationToken) : Task<IReadOnlyList<PublishedSelloutBatch>>`. It returns one entry per live batch (status 2/4) of the company with its `SalesTransaction` row count in the range, ordered by `ReportingWeekStart`, then `UploadedDate`.
  - `SelloutReplacementDto { DateTime From; DateTime To; string Summary; IReadOnlyList<PublishedSelloutBatch> Replaces; }`. `Summary` is the first verbatim copy line from Global Constraints.
  - `SelloutUploadPreflightDto.Replacement (SelloutReplacementDto?)`.

- [ ] **Step 1: Write the failing tests in `SelloutUploadServiceTests`**
- `Preflight_states_the_range_of_the_valid_rows`: valid rows on 2026-10-01 and 2026-10-07 plus an invalid row on 2026-10-20 give `Replacement.From == 2026-10-01`, `To == 2026-10-07`, and `Summary == "Confirming replaces your published sales from 01 Oct 2026 to 07 Oct 2026."`.
- `Preflight_lists_what_is_published_in_the_range`: the fake repository returns two batches, and `Replacement.Replaces` equals them in order.
- `Preflight_with_errors_neither_reads_published_ranges_nor_writes`: when there are analysis errors, `Replacement` is null, the fake records no `GetPublishedInRangeAsync` call, and nothing is inserted or committed.
- Update every `ISelloutUploadRepository` fake used by a preflight test to implement `GetPublishedInRangeAsync`, returning an empty list by default.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~SelloutUploadServiceTests`
Expected: FAIL (compile).

- [ ] **Step 3: Implement the repository query, the DTOs, the service change and the view section**

The view renders `Summary`, then one line per replaced batch, or `Nothing is published for these dates yet.` when there are none. It uses `textContent` / jQuery `.text()` only, and Confirm visibility is unchanged.

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Verify the repository query against SQL Server**

Run a temporary xUnit test (deleted before committing) on a migration-built scratch database. It must return the right per-batch counts inside a range that cuts a week in half. Drop the database.

- [ ] **Step 6: Commit**

`feat: say which Sellout dates an upload replaces before it is confirmed`
