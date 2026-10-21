# Stock Snapshot Upload Design

**Date:** 2026-08-10

## Summary

GiselX will accept company-specific Stock workbooks through the same inspect, validate, confirm, stage, and publish workflow used by Sales Transaction uploads. Each confirmed workbook represents one immutable stock snapshot for a user-selected business `StockDate`. New uploads never delete or replace earlier snapshots. The main Stock menu shows only the latest successfully published snapshot for the current company, while a separate download selector allows users to export any earlier completed snapshot belonging to that company.

Every mapped data row is retained in staging. Rows with valid required values and a non-negative `SaldoAkhir` are published to the final `Stock` table. Invalid rows, including rows with negative `SaldoAkhir`, remain in staging and are reported to the uploader without preventing valid rows from being published.

## Goals

- Support a different Stock workbook template for each distributor company.
- Require an explicit business `StockDate` for each uploaded snapshot.
- Analyze the workbook without database writes before confirmation.
- Retain every parsed mapped row in staging after confirmation.
- Publish valid rows even when other rows are invalid.
- Preserve all previous Stock snapshots without replacement or deletion.
- Show only the latest completed snapshot in the normal Stock menu.
- Allow company-scoped downloads of any completed historical snapshot.
- Maintain a durable upload audit through the Stock batch and staging records.

## Non-Goals

- Deriving the StockDate from the upload timestamp, filename, or report title.
- Replacing, deleting, or merging a previous snapshot.
- Displaying historical snapshots in the normal Stock table.
- Combining multiple StockDates in one workbook.
- Generalizing Sales Transaction and Stock into one generic upload framework.
- Adding an administrator-only upload-history page.
- Storing the original workbook bytes on disk or in SQL Server.

## User Experience

### Company-specific mapping

Administrators configure a Stock template mapping for each company from a sample workbook. The mapping records the worksheet, header row, first data row, ordered header signature, and source-column assignment for each Stock canonical field. Stock mappings are isolated from Sales Transaction mappings so the two canonical field sets cannot be confused.

During upload, GiselX searches only active Stock mappings for the current user's company and requires exactly one exact worksheet/header-signature match.

### Preflight and confirmation

The Stock upload form contains:

- a required `StockDate` picker representing the date of the stock snapshot;
- an `.xlsx` file picker; and
- an Inspect/Upload action.

The browser submits the selected date and file to an authenticated, anti-forgery-protected preflight endpoint. Preflight uses the same mapping detection, parsing, summary classification, conversion, and validation rules as the confirmed upload but performs no database writes.

The confirmation dialog shows:

- detected mapping name;
- selected StockDate;
- total mapped transaction rows;
- valid rows that will be published;
- invalid rows that will remain only in staging;
- ignored trailing summary row numbers; and
- an explicit statement that this upload creates a new snapshot and does not replace earlier snapshots.

Canceling the dialog writes nothing. Confirmation resubmits the same file and StockDate with an explicit confirmation flag. The server repeats the complete analysis and never trusts browser-provided counts or validation results.

### Upload result

The result page shows overall status, StockDate, mapping, filename, uploader, audit timestamps, total/valid/invalid counts, ignored summary rows, and a validation table containing source row, canonical field, rejected value, and reason.

An upload with at least one valid row completes even when invalid rows exist. The page clearly states that valid rows were published and invalid rows were retained only in staging.

### Latest Stock menu

The normal Stock table no longer uses Year and Month selectors. It automatically displays final Stock rows belonging to the current company's latest batch whose status is `Completed` or `CompletedWithWarnings`. “Latest” means the greatest successful upload completion time, with batch ID as the deterministic tie-breaker. The selected StockDate does not determine which upload is latest.

If a later upload fails or contains zero valid rows, the previous latest successful snapshot remains visible.

### Historical download

The Download area provides a dropdown of the current company's `Completed` and `CompletedWithWarnings` batches. Each option displays:

```text
Uploaded date/time — StockDate — original filename
```

The selected value is the `StockUploadBatchId`. The download endpoint rechecks that the batch belongs to the current company and has a downloadable status, then exports only final `Stock` rows linked to that batch. Staging-only invalid rows are not included. Failed and processing batches are never offered or downloadable.

## Canonical Stock Fields

The mapping supports the current Stock columns:

- `ProductId`
- `ProductName`
- `ProductPackaging`
- `ProductPcsInCtn`
- `ProductNetto`
- `ProductUnit`
- `SaldoAwal`
- `SaldoMasukPO`
- `SaldoAkhir`
- `BatchNumber`
- `ExpiredDate`

`ProductId`, `ProductName`, and `SaldoAkhir` are required for final publication. Other mapped fields remain optional unless a later business rule explicitly promotes them to required fields.

## Row Classification and Validation

Rows begin at the mapping's configured `FirstDataRowNumber`. Completely blank mapped rows are ignored.

A trailing row is classified as a summary row only when all of these conditions hold:

1. it occurs after at least one transaction-shaped row;
2. all mapped product identity fields (`ProductId` and `ProductName`) are blank;
3. at least one mapped numeric field contains a value; and
4. every later nonblank mapped row also meets the summary-row conditions.

Ignored summary row numbers are reported during preflight and on the result page. A summary-shaped row in the middle of data, or a trailing row containing product identity, is treated as a normal data row and staged with its validation outcome.

Every confirmed non-summary data row is inserted into `StockStaging`, including rows that fail conversion or business validation. Validation rules are:

- the file is a readable `.xlsx` within the configured upload size limit;
- exactly one active current-company Stock mapping matches the workbook;
- required canonical fields are present;
- mapped integers, decimals, and dates convert without silent fallback;
- canonical strings fit their configured maximum lengths;
- `SaldoAkhir` must be greater than or equal to zero for final publication; and
- at least one row must be valid for the snapshot to publish.

Only `SaldoAkhir` has a non-negative rule. Negative values in `ProductPcsInCtn`, `ProductNetto`, `SaldoAwal`, or `SaldoMasukPO` do not by themselves exclude a row, although conversion and required-field rules still apply.

## Data Model

### `StockTemplateMapping`

Stores one company's Stock workbook definition:

- `Id`
- `CompanyId`
- `Name`
- `WorksheetName`
- `HeaderRowNumber`
- `FirstDataRowNumber`
- `HeaderSignature`
- `IsActive`
- creator/modifier audit fields
- a collection of `StockTemplateColumn` assignments

Only one active mapping with a given exact signature may be selected during analysis. Mapping company foreign keys use restricted deletion.

### `StockTemplateColumn`

Stores the ordered source column number, source header, and Stock canonical field for its parent mapping. Source column numbers are unique within one mapping, and a canonical field may be assigned at most once.

### `StockUploadBatch`

Represents one confirmed workbook and immutable snapshot attempt:

- `Id`
- `CompanyId`
- nullable `StockTemplateMappingId` until detection succeeds
- `StockDate`
- `OriginalFileName`
- `FileSha256`
- `Status`
- `TotalRows`, `ValidRows`, `InvalidRows`
- `IgnoredSummaryRowsJson`
- `ValidationErrorsJson`
- `ErrorSummary`
- uploader and timestamp metadata

Statuses are:

- `Processing`
- `Failed`
- `Completed`
- `CompletedWithWarnings`

The batch is the durable upload log. A completed batch is never changed to “replaced,” because Stock snapshots are append-only.

### `StockStaging`

Stores every confirmed non-summary row with nullable converted Stock fields, `SourceRowNumber`, `IsValid`, and `ValidationErrorsJson`. Every staging row belongs to one `StockUploadBatch`. Staging rows are retained indefinitely and are not deleted when later snapshots arrive.

### `Stock`

The existing final table gains:

- nullable `StockUploadBatchId` for legacy compatibility; and
- nullable `StockDate` for legacy compatibility.

Every newly published final row receives both values. New rows are append-only. Existing legacy rows remain readable but are not selected as the latest snapshot unless a future explicit backfill associates them with a completed batch.

## Import and Publication Flow

1. Read bounded workbook bytes and validate the `.xlsx` extension.
2. Detect exactly one active Stock mapping for the current company.
3. Parse mapped rows and classify only qualifying trailing summary rows.
4. Convert every data row without substituting invalid values.
5. Validate required fields, lengths, and `SaldoAkhir` publication eligibility.
6. Return preflight counts and errors without writing database state.
7. After confirmation, repeat steps 1 through 5 on the server.
8. Create a `Processing` Stock batch.
9. Insert every parsed non-summary row into Stock staging.
10. If zero rows are valid, mark the batch `Failed`, commit the audit and staging diagnostics, and leave the current latest snapshot unchanged.
11. Otherwise insert only valid staging rows into final `Stock`, linked to the batch and selected StockDate.
12. Mark the batch `Completed` when all rows are valid or `CompletedWithWarnings` when any row is invalid.
13. Commit staging, final rows, and batch completion once in the existing request transaction.

An exception before the final commit rolls back the batch, staging rows, and final rows together. Because publication only appends data, no earlier snapshot is placed at risk.

## Application Boundaries

- A Stock workbook inspector detects company mappings and reads worksheet values.
- A Stock parser owns row classification, strict conversion, validation, and counts.
- A shared Stock analysis service is called by both preflight and confirmed upload.
- A Stock upload service owns bounded reading, confirmation, batch creation, staging, partial publication, status selection, and the single commit boundary.
- Stock repositories own mapping persistence, batch/staging insertion, final publication, latest-batch queries, and historical batch download queries.
- MVC endpoints own authorization, anti-forgery, current-company resolution, upload binding, result rendering, and company-scoped downloads.
- Browser code renders server-provided information with DOM text APIs and submits only the confirmation flag; it never supplies trusted counts or validation decisions.

## Security and Isolation

- Mapping detection, latest Stock display, upload result retrieval, batch dropdowns, and downloads are scoped to the authenticated user's company.
- Preflight and upload POST endpoints require the Stock upload permission and anti-forgery validation.
- Download requires the Stock download permission.
- The server accepts only `.xlsx` files within the configured size limit.
- Original filenames are treated as display metadata and never as filesystem paths.
- SQL parameters or strongly typed bulk operations are used for all database writes.

## Compatibility and Migration

The migration creates the mapping, mapping-column, batch, and staging tables; adds nullable `StockUploadBatchId` and `StockDate` to `Stock`; and creates indexes for company/latest-batch, company/StockDate, batch status, and foreign keys.

Existing Stock rows require no rewrite or deletion. Existing stored procedures used by the normal menu and downloads will be replaced or supplemented with repository queries that select by completed batch rather than `CreatedDate` month. Existing Year/Month controls are removed from the normal Stock page.

## Automated Verification

Tests will cover:

- company-scoped exact mapping detection;
- required mapping-field validation and duplicate-assignment rejection;
- strict number and date conversion;
- suffix-only summary classification;
- preflight returning counts without database writes;
- cancellation writing nothing;
- confirmed upload staging every data row;
- negative `SaldoAkhir` rows remaining staging-only;
- valid rows from the same workbook being published;
- `Completed` versus `CompletedWithWarnings` status;
- zero-valid-row failure preserving the previous latest snapshot;
- append-only behavior across repeated StockDate uploads;
- latest completed batch selection by completion time and batch ID;
- failed later uploads not changing the visible snapshot;
- historical dropdown label and ordering;
- historical download returning only valid final rows for the selected batch;
- cross-company batch access being rejected;
- anti-forgery, permissions, current-company resolution, and cancellation-token forwarding;
- migration and EF model consistency; and
- full solution build and tests.

## Acceptance Criteria

- Each distributor can configure and use its own Stock workbook template.
- Users must select a StockDate before inspecting a workbook.
- Preflight writes nothing and reports mapping, total/valid/invalid counts, and ignored summaries.
- Confirmation stages every non-summary data row.
- Rows with negative `SaldoAkhir` are shown as warnings and excluded from final Stock.
- Valid rows publish even when other rows are invalid.
- A workbook with no valid rows fails without changing the currently visible snapshot.
- Previous Stock snapshots are never deleted or replaced.
- The main Stock table displays only the latest successful upload for the current company.
- The historical download dropdown displays upload date/time, StockDate, and original filename.
- A user can download valid final rows from any completed batch belonging to their company.
- Existing legacy Stock rows remain intact.
