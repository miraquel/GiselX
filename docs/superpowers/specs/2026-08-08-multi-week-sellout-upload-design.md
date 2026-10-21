# Multi-Week Sellout Upload Design

**Date:** 2026-08-08

## Summary

GiselX will accept a distributor sellout workbook containing one or more Monday-to-Sunday reporting weeks. The application will detect the active company mapping, validate the complete workbook, calculate each transaction's reporting week from `InvoiceDate`, and show the uploader a pre-import confirmation describing every affected week. After confirmation, GiselX will replace all affected weekly datasets in one database transaction.

The upload remains all-or-nothing. If any transaction row is invalid or any weekly replacement fails, no `SalesTransaction` data from the workbook is committed and all previously active weekly data remains unchanged.

## Goals

- Accept both single-week and multi-week mapped `.xlsx` workbooks.
- Remove the need for the uploader to manually choose a reporting-week start.
- Warn the uploader when a workbook contains more than one reporting week.
- Show every affected Monday-to-Sunday period and its transaction count before replacement.
- Transparently identify ignored trailing summary rows.
- Preserve one weekly replacement unit per company and reporting week.
- Commit all affected weeks atomically.
- Keep existing single-week batch history readable.

## Non-Goals

- Partial promotion of valid weeks from an invalid workbook.
- Trusting the filename, report title, or a user-selected date to determine the reporting period.
- Combining multiple uploaded files into one import operation.
- Row-level upsert based on invoice or product business keys.
- Ignoring arbitrary malformed rows in the middle of transaction data.
- Changing the company-specific automatic mapping rules.

## User Experience

### Preflight

The Sales Transaction upload form contains an `.xlsx` file picker and an **Inspect & Upload** action. The manual reporting-week date picker is removed.

The browser submits the selected file to an authenticated, anti-forgery-protected preflight endpoint. Preflight performs the same mapping detection, parsing, footer classification, validation, and week calculation used by the final upload without modifying the database.

For a valid workbook, the page displays a confirmation dialog containing:

- detected mapping name;
- total transaction rows;
- number of detected reporting weeks;
- each Monday-to-Sunday range and transaction count;
- ignored trailing summary row numbers;
- an explicit statement that current data for every listed company/week will be replaced.

When two or more weeks are present, the dialog begins with a prominent message such as: **This workbook contains 5 reporting weeks.** A single-week workbook uses the same confirmation dialog but does not show a multi-week warning.

The user can cancel without changing any data. Confirmation resubmits the same selected file with an explicit confirmation flag. The server repeats all inspection and validation; it never trusts browser-provided week lists or counts.

### Results

The result page shows the upload group, overall status, mapping, file, total/valid/invalid counts, ignored summary rows, and a table of affected weeks with row counts and weekly batch statuses. Failed validation continues to show source row, canonical field, rejected value, and reason.

## Row Classification

Rows begin at the mapping's configured `FirstDataRowNumber`. Completely blank mapped rows remain ignored.

A trailing row is classified as a summary row only when all of these conditions hold:

1. it occurs after at least one transaction row;
2. every mapped identity field is blank: `CustomerId`, `CustomerName`, `ProductId`, `ProductName`, `InvoiceNo`, `InvoiceDate`, and `InvoiceUnit`;
3. at least one mapped numeric field contains a value; and
4. every later nonblank mapped row also meets the summary-row conditions.

This rule handles aggregate footer rows such as the GMK workbook's final quantity/value totals without hiding malformed rows embedded among transactions. Every ignored summary row is reported during preflight and on the result page.

All other nonblank mapped rows are transaction rows and receive normal validation. A transaction missing required identity values is invalid rather than silently skipped.

## Validation

The existing validation rules remain in force:

- the file is a readable `.xlsx` within the configured size limit;
- exactly one active mapping for the current company matches the worksheet and ordered header signature;
- all eight required canonical values are present on each transaction row;
- mapped integers, decimals, and dates convert without fallback;
- `InvoiceQty` is non-zero (negative quantities represent returns);
- strings fit the canonical maximum lengths;
- at least one transaction row exists.

The old rule requiring every `InvoiceDate` to match a user-selected week is removed. Instead, every valid `InvoiceDate` is converted to its Monday week start. Missing or invalid invoice dates remain row errors and therefore prevent the entire upload.

## Data Model

### `SelloutUploadGroup`

A new upload-group record represents one submitted workbook and owns the overall audit and validation result:

- `Id`
- `CompanyId`
- nullable `DistributorTemplateMappingId` until detection succeeds
- `OriginalFileName`
- `FileSha256`
- `Status` (`Processing`, `Failed`, or `Completed`)
- `TotalRows`, `ValidRows`, `InvalidRows`
- `IgnoredSummaryRowsJson`
- `ValidationErrorsJson`
- `ErrorSummary`
- uploader and timestamp metadata

Failed preflight requests do not write an upload group. A confirmed upload creates the group before repeating server-side validation, so unexpected confirmed-upload failures remain auditable.

### `SelloutUploadBatch`

`SelloutUploadBatch` remains the unit for one company and one reporting week. A nullable `SelloutUploadGroupId` foreign key links new weekly batches created from the same workbook. Existing batch rows remain valid with a null group link.

Each detected reporting week produces one child batch with its existing `ReportingWeekStart`, status, row counts, mapping, and audit metadata. File metadata remains on the group for new imports; existing columns on the batch remain populated for compatibility.

### Staging and final rows

Each transaction is staged under the child batch for its calculated week. On successful promotion, `SalesTransaction.ReportingWeekStart` equals that calculated Monday and `SelloutUploadBatchId` identifies the corresponding weekly child batch.

## Import and Promotion Flow

1. Read the bounded workbook bytes and detect exactly one active company mapping.
2. Parse mapped rows and classify only qualifying trailing aggregate rows as summaries.
3. Validate every transaction row without applying a user-selected week filter.
4. Calculate Monday week starts for valid invoice dates and build ordered week summaries.
5. Return the preflight model to the browser; do not write database state.
6. After user confirmation, repeat steps 1 through 4 on the server.
7. Create one `SelloutUploadGroup` and one `Processing` child batch for each detected week.
8. Attach each staged transaction to its calculated weekly child batch and persist all staging rows.
9. If any validation or staging error exists, mark the group and its created child batches failed, commit only the diagnostic records, and leave `SalesTransaction` untouched.
10. Otherwise, promote every child batch inside the existing request transaction. For each week, mark the previous completed batch replaced, remove its successful staging rows, delete final rows for only the same company/week, insert the new final rows, and mark the child completed.
11. Mark the group completed only after every child promotion succeeds, then commit once.

An exception during any promotion rolls back the group, all new batches, every replacement status change, every deletion, and every insertion.

## Application Boundaries

- A workbook-analysis service owns row classification, validation, week calculation, and summaries. Preflight and confirmed upload call the same service to prevent rule drift.
- The upload orchestration service owns group creation, child-batch creation, staging, and the single commit boundary.
- Repository methods insert group metadata, insert weekly batches, stage grouped rows, promote a set of weeks atomically, and retrieve grouped results.
- MVC endpoints remain responsible for authorization, anti-forgery validation, current-company resolution, upload binding, and rendering JSON or views.
- Browser code renders the server-provided summary and sends only the confirmation flag; it does not calculate trusted periods.

## Compatibility and Migration

The database migration creates `SelloutUploadGroup`, adds nullable `SelloutUploadGroupId` to `SelloutUploadBatch`, and adds the required indexes and foreign key. No existing rows need backfilling. Existing single-week batches continue to render through the current batch-result path, while grouped results render all children when the group link is present.

Existing weekly and monthly sales queries remain based on `SalesTransaction.ReportingWeekStart` and active batch status. Because each new child batch still represents exactly one week, their replacement behavior does not change.

## Automated Verification

Tests will cover:

- Monday calculation across month and year boundaries;
- one-week and multi-week grouping with deterministic ordering and counts;
- the GMK-style trailing aggregate row being ignored and reported;
- a malformed middle row remaining invalid;
- an identity-bearing trailing row remaining invalid rather than ignored;
- preflight returning mapping, week summaries, counts, and multi-week warning data;
- no database mutation during preflight;
- confirmed upload requiring an explicit multi-week confirmation;
- server-side reinspection during confirmation;
- one child batch and correct final rows per detected week;
- all affected weeks replaced for only the current company;
- failure in any week rolling back every affected week;
- invalid rows preventing all promotion;
- compatibility of existing single-week batch results;
- controller authorization, anti-forgery, company scoping, cancellation, and result rendering.

The focused test suite, complete solution tests, solution build, migration inspection, and a browser preflight/upload check using the supplied GMK workbook will be run before completion.

## Acceptance Criteria

- The uploader can select the supplied July 2026 GMK workbook without choosing a week.
- Preflight reports 305 transaction rows, 5 reporting weeks, and ignored summary row 310.
- The confirmation dialog lists the five affected week ranges and their transaction counts.
- Canceling the dialog writes nothing.
- Confirming a valid workbook stores all 305 transactions in `SalesTransaction` under five weekly child batches.
- Any invalid transaction or promotion failure stores no final transactions from the workbook and preserves all previously active weeks.
- Re-upload replaces only the current company's affected weeks.
- Existing single-week mapped uploads and historical result pages remain readable.
