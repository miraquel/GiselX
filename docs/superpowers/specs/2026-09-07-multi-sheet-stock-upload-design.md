# Multi-Sheet Stock Upload Design

Status: approved
Date: 2026-09-07

## Summary

A Stock workbook may contain more than one mapped worksheet. GiselX will accept every worksheet
whose header signature matches an active Stock template mapping of the uploading company and publish
all of their rows as **one** stock snapshot. Worksheets that match no mapping are skipped and reported
in the preflight summary instead of failing the upload.

Before this change a Stock upload required exactly one matching mapping. A workbook holding two mapped
sheets was rejected with "Multiple active Stock mappings exactly match this workbook: A, B. Deactivate
all but one." and no rows were published.

## Goals

- Publish rows from every matched worksheet of a single uploaded workbook as one `StockUploadBatch`.
- Record, per staging row, which mapping and which worksheet produced it.
- Report the per-worksheet breakdown (mapping, row counts, ignored summary rows) in the preflight
  review before the user confirms.
- Report worksheets that matched no mapping as information, not as an error.
- Keep genuine ambiguity an error: two mappings matching the *same* worksheet still fails.

## Non-Goals

- The Sellout / SalesTransaction pipeline is unchanged. Its weekly replacement semantics and its
  `UNIQUE (CompanyId, ReportingWeekStart) WHERE Status = 2` batch index stay as they are.
- No manual worksheet-to-mapping picker in the UI. Detection stays automatic, by header signature.
- No change to Stock snapshot semantics: uploads remain append-only, one confirmed workbook is one
  snapshot for the user-selected `StockDate`, and earlier snapshots are never deleted or replaced.
- No change to the `Stock` final table shape. Worksheet provenance lives in `StockStaging` only.

## Why one batch and not one batch per worksheet

The Stock menu renders exactly one batch per company — the most recently completed one
(`RankedBatches ... BatchRank = 1` in `StockRepository`). Two batches from one upload would leave the
rows of the second worksheet invisible in the grid even though they exist in the database. One batch
holding every matched worksheet's rows keeps every existing reporting query correct without change.

## Detection

`IStockWorkbookInspector.DetectAsync` returns every match instead of requiring exactly one:

- For each active mapping of the company, inspect the mapping's configured worksheet and compare the
  header signature, exactly as before.
- Each mapping that matches contributes one entry of `Matches`, carrying the mapping and the
  **resolved** worksheet name (resolution still tolerates letter-case and surrounding-whitespace
  differences in the tab name).
- Worksheets present in the workbook that no mapping matched are returned as `IgnoredWorksheetNames`.
- Zero matches produces the existing error plus the per-mapping rejection reasons from
  `DescribeRejection`.
- Two or more mappings resolving to the **same** worksheet is still an ambiguity error naming those
  mappings, because the workbook cannot say which template applies to that sheet.

Two worksheets that share an identical header layout are therefore supported: each mapping only ever
reads the worksheet it points at, so identical signatures on different tabs do not collide.

## Analysis

`StockWorkbookAnalysisService.AnalyzeAsync` parses each matched worksheet with its own mapping, tags
every produced row with its `StockTemplateMappingId` and `SourceWorksheetName`, resolves item mappings
once for the company, and concatenates the results.

- `StockWorkbookAnalysisDto.Sheets` reports per worksheet: worksheet name, mapping id and name, total
  rows, valid rows, invalid rows, and ignored summary row numbers.
- `StockWorkbookAnalysisDto.Mapping` stays populated when exactly one worksheet matched, so the batch
  keeps recording its mapping in that case, and is `null` for a multi-worksheet upload.
- A matched worksheet with no data rows is reported as a zero-row entry in `Sheets`. The batch-level
  "The workbook contains no data rows." error is raised only when no matched worksheet produced a row.
- Row-level errors carry `WorksheetName`, because `SourceRowNumber` alone is ambiguous across sheets.

## Data Model

`StockStaging` gains two nullable columns:

| Column | Type | Notes |
| --- | --- | --- |
| `StockTemplateMappingId` | `int NULL` | FK to `StockTemplateMapping`, `ON DELETE NO ACTION`. Null for rows staged before this change. |
| `SourceWorksheetName` | `nvarchar(255) NULL` | Resolved worksheet name the row was read from. |

`StockUploadBatch.StockTemplateMappingId` already allows null and now means "one mapping produced this
batch"; it is null when a batch spans several mappings. `Stock`, `StockUploadBatch`, and the sellout
tables are otherwise untouched.

`StockUploadBatch.IgnoredSummaryRowsJson` keeps its existing flat `int[]` shape and holds the union of
ignored summary row numbers across worksheets. The per-worksheet breakdown is a preflight concern; the
batch audit record intentionally keeps the shape its consumers already parse.

## User Experience

The preflight review modal keeps its existing totals and gains:

- a **Worksheets** table listing each matched worksheet with its mapping name and row counts;
- a line naming worksheets that were skipped because no mapping matched them;
- "Detected mapping" showing every matched mapping name when there is more than one.

Confirming publishes one snapshot exactly as before.

## Compatibility and Migration

- EF migration `AddStockStagingSheetSource` adds both nullable columns and the FK.
- Companion manual installer `GiselX.Web/DatabaseScripts/20260907024804_AddStockStagingSheetSource.sql`
  mirrors it under `SET XACT_ABORT ON`, an `sp_getapplock`, existence guards, no destructive DML, and an
  `INSERT INTO dbo.__EFMigrationsHistory` inside the transaction.
- Existing staging rows keep `NULL` in both columns; nothing backfills them.
- A single-worksheet workbook behaves exactly as before, including the batch's recorded mapping id.

## Automated Verification

- Detection: two mappings on different worksheets both match; two mappings on the same worksheet stay
  an ambiguity error; unmatched worksheets are reported, not fatal.
- Analysis: rows from both worksheets are returned, each tagged with its mapping and worksheet, and the
  per-worksheet summary reports the right counts.
- Upload: staging rows persist mapping id and worksheet name; a multi-worksheet batch records a null
  `StockTemplateMappingId`; a single-worksheet batch still records its mapping.
- Persistence model: both columns exist with the approved type, nullability, and delete behavior.
- Manual SQL script: transaction, applock, existence guards, column type checks, migration-history row.

## Acceptance Criteria

1. A workbook with two mapped worksheets uploads in one action and publishes the rows of both.
2. A workbook with one mapped worksheet and one unmapped worksheet uploads, and the preflight names
   the skipped worksheet.
3. Two mappings pointing at the same worksheet still fail with the ambiguity error.
4. A workbook matching no mapping still fails with the per-mapping rejection reasons.
5. Every published row's staging row records the worksheet and mapping it came from.
6. Earlier Stock snapshots are never deleted or replaced.
