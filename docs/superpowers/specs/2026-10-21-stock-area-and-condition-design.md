# Stock Area and Condition Design

Status: approved
Date: 2026-10-21

## Summary

A Stock row gains two attributes read from the workbook itself: the **Area** (the distributor's
warehouse or location) and the **Condition** (Good stock or Bad stock). Both arrive as columns on each
data row and are mapped by the Stock template like any other column.

Publishing a Stock upload then replaces only the **areas the upload contains**, for its snapshot date,
instead of the distributor's whole snapshot for that date. A distributor whose areas send their files on
different days, uploaded separately, no longer has one area's upload wipe out another's.

## Why

PT. Primatrans keeps stock in more than one location. Each location sends its own stock file, on its
own day, and each file is uploaded on its own. Today a Stock upload is one snapshot per distributor per
`StockDate`:

- `StockUploadRepository.PublishValidRowsAsync` deletes every earlier batch's rows for the same
  `(CompanyId, StockDate)` and marks those batches Replaced, so the second location's upload deletes the
  first location's rows.
- `StockSnapshotPeriod.RankedBatches` shows one batch per distributor per month, so giving the two files
  different dates does not help: one location is hidden from the Stock page.
- `vw_GiselStock_Live` in `DatabaseScripts/Reporting_StockAndSellout.sql` keeps one batch per
  `(CompanyId, StockDate)`, so the same location is missing from Power BI.

The same distributor's rows also mix good and bad stock, and PT. GMK needs to see them separately.

Considered and rejected:

- **Several files in one upload.** This was the first option chosen. It fails once the locations send
  their files on different days, which they do.
- **Location chosen at upload time, or read from the file name.** Both were superseded once it was
  established that the area is a column on every row. A row-level value cannot be mis-chosen by the
  uploader and also covers one file that holds several areas.

## Goals

- Store an Area and a Condition on every Stock row, and on every staging row.
- Read both from mapped columns. A template that maps neither behaves exactly as today.
- Replace only the published rows of the areas present in the upload, for the upload's `StockDate`.
- Show the latest snapshot of **each area** on the Stock page for a chosen month, with Area and
  Condition as columns that can be filtered.
- Expose Area and Condition in `vw_GiselStock_Live`.
- Before confirming, show which areas the upload replaces and what it replaces.

## Context: before go-live

GISEL's Sellout and Stock uploads go live in **November 2026**. No production Stock data exists for any
distributor yet. **No transition handling is needed** for rows published without an area before a
company registered its areas.

One guard keeps the two kinds of rows from mixing later:

- If the uploading company has at least one active area, every matched template must map `Area`.
- An upload whose matched template does not map `Area` is refused at preview with a message that names
  the template.
- This guard was confirmed in conversation. Existing pre-go-live data is ignored; the system starts
  from new uploads that carry an area and a Good/Bad condition.

## Non-goals

- **No migration or reconciliation of no-area rows.** Production has none; see the context above.
- **Sellout / SalesTransaction is unchanged.**
- **No upload of several files at once.** Each file is its own upload.
- **No area or condition taken from the file name, the worksheet name or an upload-form choice.**
- **No per-area permissions.** A Distributor user still sees every area of their own company.
- **No change to item mapping, reminders or the upload check.**

## Anti-goals

Any of these would be a failure even if the feature technically works:

- An upload for one area deletes or hides another area's published rows.
- A row whose area or condition cannot be resolved is silently counted. In particular, it must not be
  counted as Good.
- `Medan`, `MEDAN` and `Gdg Medan` appear as three different areas in a report.
- Existing distributors with a single location see any change in what is published, replaced or shown.

## Data model

### Area master: `StockArea`

A new table, owned per company:

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | int, identity | |
| `CompanyId` | int | FK to Company, restrict |
| `Name` | nvarchar(100) | Display name, unique per company |
| `Aliases` | nvarchar(1000), nullable | `;`-delimited alternative spellings |
| `IsActive` | bit | |
| `CreatedDate`, `CreatedBy`, `ModifiedDate`, `ModifiedBy` | | Audit columns, as on other master tables |

- An inactive area resolves no rows.
- Deleting an area that rows reference is refused, as for other master data. Deactivate it instead.

### Condition

`StockCondition` is an enum stored as `tinyint`: `Good = 0`, `Bad = 1`.

### New columns

On `Stock` and `StockStaging`:

| Column | Type | Meaning |
| --- | --- | --- |
| `StockAreaId` | int, nullable, FK to `StockArea` (restrict) | Null means "no area". |
| `Condition` | tinyint, not null, default 0 | Good / Bad |

`StockStaging` additionally keeps the raw cell text in `SourceAreaText` and `SourceConditionText`,
nvarchar(200), nullable. This lets an invalid row show what the workbook actually said.

On `StockTemplateMapping`:

| Column | Type | Meaning |
| --- | --- | --- |
| `GoodConditionValues` | nvarchar(500), nullable | `;`-delimited cell values that mean Good |
| `BadConditionValues` | nvarchar(500), nullable | `;`-delimited cell values that mean Bad |

**Backfill:** existing `Stock` and `StockStaging` rows get `Condition = 0` (Good) and `StockAreaId = NULL`.
No existing row changes meaning.

### Canonical fields

`StockCanonicalField` gains two optional fields:

- `Area`, text.
- `Condition`, text.

Neither is added to `RequiredFields`.

## Resolution rules

Resolution runs during analysis, so the preview shows the result before anything is written.

Normalisation compares two values by trimming them, collapsing internal whitespace and ignoring case.

### Area

**Template does not map `Area`:** every row gets `StockAreaId = NULL`.

**Template maps `Area`:** the cell is normalised and compared with each **active** area of the
uploading company, on both `Name` and every entry of `Aliases`.

- **Exactly one area matches:** the row gets that area.
- **No area matches:** the row is invalid. Field `Area`, reason `Area '<text>' is not registered for <company>.`
- **More than one area matches:** the row is invalid with an ambiguity reason. This is a configuration
  error and the message says so.
- **The cell is blank:** the row is invalid. Field `Area`, reason `Area is empty.`

### Condition

**Template does not map `Condition`:** every row is Good.

**Template maps `Condition`:** the normalised cell is compared with the template's
`GoodConditionValues` and `BadConditionValues`.

- **Found in one list only:** the row gets that condition.
- **Found in neither list:** the row is invalid, and the reason names the value.
- **Found in both lists:** this is a template configuration error. It is rejected when the template is
  saved, so it cannot reach an upload.
- **The cell is blank:** the row is invalid. A blank must never default to Good.

An invalid row is staged with its errors and not published, exactly as invalid rows are today. Stock
already accepts partial success, so the batch ends `CompletedWithWarnings`.

## Publication and replacement

The replacement scope of an upload is the set of `StockAreaId` values carried by its **valid** rows,
where "no area" counts as one value.

`PublishValidRowsAsync` changes from "delete every earlier batch's rows for this company and date" to
"delete earlier rows for this company and date **whose area is in the scope**":

```sql
DELETE stock FROM Stock stock
JOIN StockUploadBatch prior ON prior.Id = stock.StockUploadBatchId
WHERE prior.CompanyId=@CompanyId AND prior.StockDate=@StockDate
  AND prior.Id<>@BatchId AND prior.Status IN (2,3)
  AND EXISTS (SELECT 1 FROM StockStaging s
              WHERE s.StockUploadBatchId=@BatchId AND s.IsValid=1
                AND ((s.StockAreaId IS NULL AND stock.StockAreaId IS NULL) OR s.StockAreaId=stock.StockAreaId));
```

The whole area is replaced, both Good and Bad rows. A location's file is the full picture of that
location, so a product or condition missing from the new file is genuinely gone.

A prior batch becomes Replaced (status 4) only when **none** of its published rows remain after the
delete. A batch that carried several areas and lost only some of them stays live with its remaining
rows.

**Consequence for existing distributors.** Their uploads have no area, so the scope is always
`{no area}` and the result is identical to today's behaviour.

**Rows of an area whose rows are all invalid** are outside the scope, so that area's earlier rows for
the date survive. An upload never empties an area it did not replace with data.

## Reading

### Stock page grid, count and summary

`StockSnapshotPeriod` currently ranks **batches** per company. It must rank per **company and area**
instead. For each `(CompanyId, StockAreaId)`, the shown rows are those of the latest `StockDate` inside
the chosen month. Among batches on that date, the latest completed one wins, which keeps the existing
protection against legacy duplicate snapshots. Without a chosen month the rule is the same with no date
window.

The grid, its count and the summary tiles keep sharing one CTE, as they do today.

**Grid:** gains Area and Condition columns. Both can be filtered through the existing column-filter
mechanism.

**Summary:**
- The existing tiles total every area and both conditions.
- Two tiles are added: **Saldo Akhir Good** and **Saldo Akhir Bad**.

**Batch download.** The page has no export of the grid itself. The one Stock Excel export is
`StocksController.DownloadBatch` (`Stocks.Download` permission), which downloads one completed batch
built by `StockRepository.SelectByBatchAsync`. That download gains Area and Condition columns.

A batch that has been partly replaced stays in the batch dropdown and exports only the areas it still
holds. This is the builder's call and is not a new export.

### `vw_GiselStock_Live`

- The live rank partitions by `(CompanyId, StockDate, StockAreaId)` instead of
  `(CompanyId, StockDate)`.
- The view adds `AreaName` and `Condition`. `Condition` is emitted as text, `Good` / `Bad`, so Power BI
  needs no lookup.
- `Reporting_StockAndSellout.sql` stays outside the migration chain and is re-run to update.

## Upload preview

The preview adds an **Areas** section that lists, for the chosen `StockDate`:

- each area the upload will replace, with its valid row count and its Good/Bad Saldo Akhir in this
  upload;
- for each of those areas, what is currently published for that area and date, if anything: row count,
  upload time, uploader and file name. The section states that confirming replaces it;
- other areas already published for that date that this upload leaves untouched.

For a distributor without areas, the same section shows the single "no area" snapshot that will be
replaced. That replacement is silent today.

Row errors show the area and condition text the cell held, through the existing `RejectedValue`.

## Administration

**Areas** are managed from the Company admin screen, under the existing Companies edit permission:
add, rename, edit aliases, deactivate. Aliases go through the same validation-and-normalise discipline
as the `;`-delimited e-mail lists. They are split, trimmed and de-duplicated, and an alias that equals
another active area's name or alias in the same company is rejected.

**Stock template editor:**
- `Area` and `Condition` appear among the canonical fields.
- When `Condition` is mapped, the editor shows two inputs, "Values meaning Good" and "Values meaning
  Bad".
- Proposed defaults are `GOOD;GS;BAIK` and `BAD;BS;BAD STOCK;RUSAK`. They are a starting point to be
  confirmed against real files.

## Deployment

- One EF migration creates `StockArea`, adds the new columns, and backfills `Condition` through the
  column default.
- A companion manual script goes in `DatabaseScripts/`, following the conventions in `CLAUDE.md`: an
  explicit transaction, `sp_getapplock`, existence guards, and the history insert. Any statement that
  references a column added in the same script goes through `sp_executesql`.
- `Reporting_StockAndSellout.sql` is updated and re-run separately.
- There are no stored procedures to change. `GiselStock_*` procs are not called by any live path.

## Verification

The suite cannot execute SQL. Beyond unit tests of resolution, scope and query shape, the change must be
run against a real database:

- Upload area A, then area B for the same date on a later day. Both are published and both are shown.
- Re-upload A. Only A is replaced.
- Upload A and B for different dates in the same month. Both are shown, each at its own latest date.
- An existing single-location distributor uploads twice for one date. The second replaces the first,
  exactly as today.
- Filter `vw_GiselStock_Live` to the snapshot dates the Stock page shows for each area in a month. The
  row counts and Saldo Akhir totals must then match the page. The view returns every live snapshot date,
  and that is unchanged, so this check is a comparison of those dates only. It does not add a
  "latest in month" view.

## Left to the builder

- Exact names of DTO members, the admin screen layout, and how the Area section is laid out in the
  preview modal.
- Whether `StockArea` uniqueness is enforced by a filtered unique index or by the service. Both are
  required behaviour; the mechanism is open.
- Index choices on the new columns.
