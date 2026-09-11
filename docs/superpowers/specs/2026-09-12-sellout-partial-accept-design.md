# Sellout Partial Accept Design

**Date:** 2026-09-12
**Status:** approved

## Summary

A Sellout upload is all-or-nothing: `SelloutWorkbookAnalysisDto.IsValid` requires `RowErrors.Count == 0`,
and `PromoteGroupAsync` throws if any staging row carries `IsValid = 0`. One unreadable cell in row 7,432
therefore rejects an entire month, and the distributor must repair the file and start again. The Stock
pipeline already does the opposite: it publishes its valid rows and reports the rest.

Sellout will publish its valid rows too, and reject the upload only when the share of invalid rows crosses
a configured threshold. Below the threshold the upload completes with warnings; at or above it the group
fails exactly as it does today, because a file that broken is a mapping or export problem rather than a
few bad cells.

## Goals

- Publish the valid rows of a workbook that contains a small number of bad ones.
- Refuse a workbook whose invalid share crosses a configurable threshold, and say so in those terms.
- Keep every invalid row and its reason in the upload audit.
- Distinguish a clean upload from one that completed with rows excluded.
- Leave week replacement, multi-week confirmation and the partial-week warning unchanged.

## Non-Goals

- Letting the uploader choose the threshold per upload. It is company-wide configuration.
- Repairing or guessing at a bad cell.
- Changing which rows are considered invalid; only what happens to them afterwards.

## Threshold

`SelloutUpload:MaxInvalidRowPercentage`, default **5**. The upload fails when

    invalid rows / total parsed rows * 100 >= MaxInvalidRowPercentage

Setting it to `0` restores today's all-or-nothing behaviour, which is the escape hatch if partial accept
turns out to be wrong for a company. Setting it to `100` accepts any workbook that parsed at all.

The `SelloutUpload` configuration section does not currently exist in `appsettings.json` even though
`SelloutUploadOptions` binds it; it is added alongside `MaxFileSizeBytes` so both settings are visible.

## Row placement

Staging rows hang off a week batch, and week batches are derived from rows that have a valid invoice date.
An invalid row often has no usable date — that is frequently why it is invalid — so it cannot always be
placed:

| Row | Placement |
| --- | --- |
| Valid | staged against its week's batch, published to `SalesTransaction` |
| Invalid, invoice date resolves to a week that has a batch | staged against that batch, never published |
| Invalid, no invoice date, or a week no valid row reached | not staged; reported through the group |

Nothing is lost in the third case: the group's `ValidationErrorsJson` already carries every row error from
the analysis, and the upload result screen reads the group's errors alongside the batches'.

This also fixes a latent crash. `UploadAsync` places every row with `batchIds[GetMonday(row.InvoiceDate!.Value)]`,
which throws on a null date and on a week with no batch. It is unreachable today only because the method
is not entered unless every row is valid.

## Status

Both status enums gain a value rather than reusing `Completed`, so a reader can tell the two apart:

- `SelloutUploadBatchStatus.CompletedWithWarnings = 4` (3 is `Replaced`)
- `SelloutUploadGroupStatus.CompletedWithWarnings = 3`

No migration: the columns are `int` and carry no check constraint. Existing rows keep their meaning.

## Publication

`PromoteGroupAsync` changes in three ways:

1. The `THROW 50003` guard on invalid staging rows is removed — invalid rows are now expected.
2. The insert into `SalesTransaction` already filters `s.IsValid = 1` and is unchanged.
3. Batch and group completion record the real `InvalidRows` count instead of `0`, and set
   `CompletedWithWarnings` when that count is above zero.

Week replacement is unchanged and still replaces each covered week in full. A week that contributes invalid
rows therefore publishes fewer rows than the file holds, which is the intended trade and is reported as the
invalid count on that batch.

## User-visible result

The completion message names what was excluded, mirroring Stock's wording:

> Sellout data was replaced for every reporting week in the workbook, with 12 invalid row(s) excluded.

A rejected upload names the proportion and the limit:

> 431 of 2,140 row(s) (20.1%) failed validation, which is at or above the 5% limit. No data was published.

## Automated Verification

- A workbook with one invalid row out of many publishes the valid rows and completes with warnings.
- The same workbook records the invalid row on its week batch and reports its reason.
- A workbook whose invalid share reaches the threshold fails, publishes nothing, and names the proportion.
- A threshold of `0` rejects a workbook with a single invalid row.
- An invalid row with no invoice date does not prevent the upload and is reported through the group.
- An invalid row in a week no valid row reached does not prevent the upload.
- A fully valid workbook still completes as `Completed`, not `CompletedWithWarnings`.

## Acceptance Criteria

- No database migration accompanies this change.
- Week replacement, multi-week confirmation and partial-week warnings behave as before.
- `MaxInvalidRowPercentage = 0` reproduces today's behaviour exactly.
