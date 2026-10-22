# Sellout Month View and Date-Range Replacement Design

Status: approved
Date: 2026-10-22

## Summary

Two corrections to Sellout before the November 2026 go-live:

1. **Month view shows every live row.** The Sellout grid, its summary tiles and its Excel download show
   all published rows of the chosen month. Today they show only the rows of each company's latest
   upload in that month.
2. **An upload replaces only the dates it contains.** Publishing a Sellout upload replaces the
   company's published rows for the date range covered by the upload's valid rows. Today it replaces
   every ISO week the upload touches, in full.

Rows are still grouped into ISO weeks (Monday–Sunday) as before, and one batch is still created per
week.

## Why

**Month view.** Every Sellout reader keeps only rows whose `CreatedDate` equals the company's maximum
`CreatedDate` inside the chosen month:

- `GiselSales_Select` backs a Distributor's grid.
- `GiselSales_SelectByCustPeriod` backs the Excel download.
- `SalesTransactionRepository.SelectAllAsync` / `SelectAllByPeriodAsync` back the Administrator grid
  and download.
- `SalesSelectionSummaryQuery` backs the tiles.

Each upload stamps its rows with one `SYSUTCDATETIME()` at publication. A distributor who uploads
weekly therefore sees only the last week of a month.

The rule predates week supersession. It was the guard against the same data appearing twice, and
supersession now does that job. `vw_GiselStock_Live`'s Sellout counterpart, `vw_GiselSales_Live`, has
no such filter, so the app and Power BI disagree for the same month.

**Replacement.** `SelloutUploadRepository.PromoteGroupAsync` deletes, for every reporting week of the
new upload, all of the company's rows in that whole Monday–Sunday week. A file that starts on the 1st
in mid-week therefore deletes the last days of the previous month.

Example: a file for 1–7 Oct 2026 replaces ISO week 28 Sep–4 Oct and deletes 28–30 Sep. Distributors
that send cumulative month-to-date files (1–7, 1–14, 1–21 …) hit this on every upload. Asking them to
change their files was judged impractical. The preview warns about it (`PartialWeekWarnings`), but
the warning does not block the upload.

## Goals

- The Sellout month view (grid, count, tiles, download) for both Administrators and Distributors
  shows every row of live batches whose `InvoiceDate` falls in the month. It agrees with
  `vw_GiselSales_Live` for the same month and company.
- Publication deletes exactly the company's published rows whose invoice date lies within
  [earliest, latest] invoice date of the upload's **valid** rows, then inserts the new valid rows.
- A prior batch is marked Replaced only when none of its rows remain. A batch that lost only part of
  its week stays live with the rest.
- Before confirming, the preview states the date range that will be replaced and what is currently
  published inside it.

## Non-goals

- No change to grouping by ISO week, to one batch per week, to the multi-week confirmation, or to
  `MaxInvalidRowPercentage`. Point 2 of the go-live review, the 5% tolerance against the hidden
  Confirm button, is deliberately deferred.
- No restoring of rows already deleted by the old rule. Production holds no Sellout data yet.
- No change to Stock, reminders or notifications.

## Anti-goals

- An upload deletes rows outside the invoice-date range of its valid rows.
- The month view shows the same transaction twice.
- A distributor uploading full Monday–Sunday weeks sees any change in what is replaced or shown.

## Rules

### Replacement range

- **From** = the earliest `InvoiceDate` (date part) among the group's valid staging rows.
- **To** = the latest one.
- Comparison is by date: `InvoiceDate >= @From AND InvoiceDate < DATEADD(day, 1, @To)`.
- A day inside the range with no rows in the file genuinely had no sales, so its old rows are deleted.
- Invalid rows do not widen the range. An upload with no valid rows never publishes (unchanged).

### What is deleted

Every `SalesTransaction` row of the company with an invoice date in the range is deleted. This
includes legacy rows with no batch. Today's delete already covers rows with a null
`ReportingWeekStart` by invoice date.

### Prior batch status

- **When:** after the delete and before the insert.
- **Which batches:** a prior Sellout batch of the company that is live, **had rows inside the
  replaced range before the delete**, and now has no `SalesTransaction` rows. Live is status `2`
  Completed or `4` CompletedWithWarnings, using the Sellout enum, where `3` is Replaced. Limiting the
  check to batches this publish actually touched keeps it from re-labelling unrelated batches. This is
  my call.
- **What happens:**
  - The batch is set to `3`.
  - Its staging rows are deleted, as replaced batches' staging rows are deleted today.
- **Other batches:** batches that keep some rows stay live, keep their status and keep their staging
  rows.

### Concurrency

Take a transaction-owned exclusive `sp_getapplock` per company at the start of `PromoteGroupAsync`,
after the company is read. Two publishes with overlapping ranges then serialise. This matches what
Stock publication now does per company and date.

### Uniqueness of live batches

The filtered unique index on `SelloutUploadBatch (CompanyId, ReportingWeekStart) WHERE Status = 2` no
longer holds, because two batches can now be live for the same week (the September part and the
October part of week 28 Sep–4 Oct). Replace it with a non-unique index on
`(CompanyId, ReportingWeekStart)`.

### Month view

The month view is every row of the company, or of all companies for an Administrator, whose invoice
date is in the chosen year and month and whose batch is live: `SelloutUploadBatchId` refers to a
batch with status 2 or 4. This is the same rule `vw_GiselSales_Live` applies. For legacy rows with no
batch, see "Left to the builder".

The `CreatedDate = MAX(CreatedDate)` filter is removed from:

- `GiselSales_Select`
- `GiselSales_SelectByCustPeriod`
- `SalesTransactionRepository.SelectAllAsync`
- `SalesTransactionRepository.SelectAllByPeriodAsync`
- `SalesSelectionSummaryQuery`

The grid, its count and the tiles must keep totalling the same rows.

## Preview

The preview's partial-week warnings are removed. In their place the preview shows a
**Replacement** section:

- *"Confirming replaces your published sales from <From> to <To>."*
- For each currently live batch that has rows in that range: the reporting week, the number of rows
  in the range, the upload time, the uploader and the file name.
- When nothing is published in the range: *"Nothing is published for these dates yet."*

A repository read supplies it: `GetPublishedInRangeAsync(companyId, from, to)` returns one entry per
live batch with its row count inside the range. The preview performs no writes, as today. It reads
only when the analysis has no errors.

## Deployment

- **One EF migration.** It installs the changed `GiselSales_Select` and `GiselSales_SelectByCustPeriod`
  with `CREATE OR ALTER`, byte-for-byte the readable sources in `StoredProcedures/`. Its `Down`
  restores the previous bodies. It also swaps the batch index.
- **Companion manual script** in `DatabaseScripts/`, following CLAUDE.md:
  - an explicit transaction, an applock, guards and the history insert;
  - procedure bodies through `sp_executesql`;
  - a guarded drop of the filtered unique index and a guarded create of the plain one.
- **`StoredProcedureDeploymentTests`** must keep passing.
- **`vw_GiselSales_Live`** needs no change, because it already shows every live row.

## Verification

The suite cannot execute SQL. Beyond unit and SQL-shape tests, run the following against a scratch
database built from migrations, never the developer's `GiselXDb`:

1. Weekly uploads W1–W4 of one month: the month view shows all four weeks. Grid, tiles and download
   agree with each other and with `vw_GiselSales_Live`.
2. Cumulative files 1–7 Oct, then 1–14 Oct, with September already published through 30 Sep:
   - 28–30 Sep survive;
   - 1–7 Oct is replaced, not doubled;
   - the first October batch for week 28 Sep–4 Oct is Replaced only once all its rows are gone;
   - the September batch for that week stays live.
3. A full Monday–Sunday re-upload of one week behaves exactly as before.
4. Two batches live for the same week insert without a unique-index violation.

## Known consequence

A batch that keeps only part of its rows still records its original `ValidRows`, its staging rows
and its result page. Batch details and the upload e-mail describe the upload as it was made, not what
is still published. This is accepted. Batch rows are audit history, as CLAUDE.md says.

## Left to the builder

- DTO and member names for the Replacement preview section, and its layout in the modal.
- Whether the month view keeps legacy no-batch rows. The direct-upload path that created them is no
  longer reachable from any controller and production has none. Either is acceptable if grid, count,
  tiles and download agree.
