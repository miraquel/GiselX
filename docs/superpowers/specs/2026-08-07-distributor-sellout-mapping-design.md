# Distributor Sellout Template Mapping and Staging Design

**Date:** 2026-08-07

## Summary

GiselX will support company-specific Excel sellout templates instead of requiring every distributor to use the existing fixed 24-column workbook. An administrator will create one or more named mappings for each company from a sample workbook. Each mapping records the worksheet, header row, first data row, and the source-column-to-canonical-field assignments.

For every weekly upload, GiselX will automatically identify exactly one active mapping, convert the uploaded rows into a typed staging table, validate the entire batch, and promote it into the existing `SalesTransaction` table only when every row is valid. `SalesTransaction` remains the physical final sellout table; no second final sellout table will be introduced.

One company submits one complete sellout file for each Monday-through-Sunday reporting week. A successful re-upload replaces the company's prior data for that week. A failed upload never changes the current sellout data.

## Goals

- Support multiple named Excel layouts for each company.
- Configure the worksheet, header row, and first data row independently for every mapping.
- Automatically identify a mapping from the uploaded workbook without asking the uploader to select one.
- Require only the agreed core sellout fields while allowing optional canonical fields to remain empty.
- Persist uploaded data in a typed staging table before creating final sellout rows.
- Validate an entire batch and promote it atomically.
- Replace the previous complete weekly sellout batch on a successful re-upload.
- Provide row- and field-specific validation errors.
- Support both weekly and monthly sellout views and downloads.
- Preserve existing historical sellout records created before weekly batch metadata exists.

## Non-Goals

- Fixed or calculated default values for unmapped fields.
- Multiple files combined into one company's weekly sellout dataset.
- Partial promotion of valid rows from an invalid batch.
- Row-by-row upsert based on invoice or product business keys.
- Per-distributor staging table schemas.
- Long-term storage of the uploaded Excel binary.
- Continued implicit support for the old GiselX template. A company must explicitly save that layout as a named mapping if it wants to use it.

## Existing System Impact

The existing `SalesTransaction` entity and SQL table already represent sellout data and will remain the canonical final store. The current upload controller assumes a fixed 24-column workbook and inserts directly into `SalesTransaction`; this path will be replaced by mapped staging and promotion.

Existing sellout queries select only the latest `CreatedDate` batch within a month. That behavior cannot represent four or five active weekly batches in the same month. The sellout queries and downloads will therefore distinguish legacy rows from new weekly rows:

- mapped uploads use explicit reporting-week and upload-batch metadata;
- legacy rows retain the previous latest-upload behavior;
- legacy rows are excluded for a week once a mapped weekly upload has replaced that week.

## Canonical Sellout Fields

The canonical fields are the existing `SalesTransaction` fields.

The following eight fields are required in every saved mapping and every staged row:

1. `CustomerId`
2. `CustomerName`
3. `ProductId`
4. `ProductName`
5. `InvoiceNo`
6. `InvoiceDate`
7. `InvoiceQty`
8. `InvoiceUnit`

All other `SalesTransaction` fields are optional. An optional field may be mapped from a source column or left unmapped. Unmapped optional values are persisted using the existing final-table conventions for empty strings, zero numeric values, or the existing minimum-date sentinel where the final model does not permit null. The staging representation remains nullable so absence can be distinguished from a supplied invalid value during validation.

A mapping cannot assign two source columns to the same canonical field or one source column to multiple canonical fields.

## Data Model

### `DistributorTemplateMapping`

Stores a named workbook layout for one company.

- `Id`
- `CompanyId`
- `Name`
- `WorksheetName`
- `HeaderRowNumber`
- `FirstDataRowNumber`
- `HeaderSignature`
- `IsActive`
- audit timestamps and user identifiers

Rules:

- Mapping names are unique within a company, case-insensitively.
- `HeaderRowNumber` and `FirstDataRowNumber` are positive.
- `FirstDataRowNumber` must be greater than `HeaderRowNumber`.
- A mapping referenced by upload history cannot be deleted; it can only be deactivated.

### `DistributorTemplateColumn`

Stores a single source-to-canonical assignment.

- `Id`
- `DistributorTemplateMappingId`
- `SourceColumnNumber`
- `SourceHeader`
- `CanonicalField`

Database uniqueness constraints prevent duplicate source-column and canonical-field assignments inside one mapping.

### `SelloutUploadBatch`

Tracks one upload attempt.

- `Id`
- `CompanyId`
- nullable `DistributorTemplateMappingId` until detection succeeds
- `ReportingWeekStart`
- `OriginalFileName`
- `FileSha256`
- `Status`
- `TotalRows`
- `ValidRows`
- `InvalidRows`
- `ErrorSummary`
- uploader and timestamp metadata

`ReportingWeekStart` must be a Monday. Batch statuses are:

- `Processing`
- `Failed`
- `Completed`
- `Replaced`

Only one batch may have `Completed` status for a company and reporting week. The previous completed batch is marked `Replaced` inside the same transaction that promotes its replacement.

### `SelloutStaging`

Stores canonical, nullable values for a parsed workbook row.

- `Id`
- `SelloutUploadBatchId`
- `SourceRowNumber`
- nullable versions of all canonical `SalesTransaction` data fields
- `IsValid`
- structured validation-error data containing field, rejected value, and reason

Failed batch staging rows remain available for diagnosis. When a completed weekly batch is successfully replaced, the previous successful batch's staging rows are deleted while its batch metadata remains as audit history.

### `SalesTransaction` additions

- nullable `SelloutUploadBatchId` foreign key
- nullable `ReportingWeekStart`

The columns are nullable so existing historical records remain valid. New mapped records always populate both columns. An index on `CompanyId, ReportingWeekStart` supports weekly replacement and querying.

## Mapping Management Flow

1. From Edit Company, an administrator opens the Sellout Template Mappings section.
2. The administrator creates a named mapping and uploads a sample `.xlsx` workbook.
3. GiselX reads workbook metadata and lets the administrator select a worksheet.
4. The administrator sets the header-row and first-data-row numbers.
5. GiselX previews the selected rows and source headers.
6. The administrator maps source columns to canonical sellout fields.
7. GiselX validates uniqueness and verifies that all eight core fields are mapped.
8. GiselX saves the mapping and an ordered normalized header signature.

Header normalization trims leading and trailing whitespace, collapses repeated internal whitespace, and compares case-insensitively. Trailing blank worksheet cells do not participate in the signature. Duplicate normalized headers are rejected because they make automatic detection ambiguous.

The complete ordered signature is used, so adding, removing, renaming, or reordering a source column requires updating or creating a mapping.

## Automatic Mapping Detection

When a company uploads a sellout workbook, GiselX evaluates every active mapping for that company:

1. Locate the mapping's configured worksheet.
2. Read the mapping's configured header row.
3. Normalize its ordered headers.
4. Compare the result to the stored signature.

Exactly one mapping must match. No match produces a batch-level error explaining that the distributor template is not configured. More than one match produces an ambiguity error listing the matching mapping names. Neither condition creates or replaces final sellout rows.

## Weekly Upload and Promotion Flow

1. The uploader selects a Monday week-start and an `.xlsx` file.
2. GiselX creates a `Processing` batch record with filename, SHA-256 hash, company, week, and uploader metadata.
3. GiselX automatically detects exactly one active company mapping.
4. Starting at the configured first data row, GiselX ignores completely blank rows and maps populated rows into nullable typed staging records.
5. Parsing and business validation errors are attached to their source row and canonical field.
6. All staging rows are saved for the batch.
7. If any row is invalid, the batch becomes `Failed`; the active final sellout dataset is untouched.
8. If all rows are valid, GiselX begins one database transaction.
9. Inside the transaction, GiselX marks the previous completed batch for the company and week as `Replaced`, removes its successful staging rows, and deletes final sellout rows for the same company and reporting week. Legacy untagged rows with invoice dates in that week are also removed because the new file is a complete replacement.
10. GiselX inserts final `SalesTransaction` rows linked to the new batch and week.
11. GiselX marks the new batch `Completed` and commits.

Any failure during replacement or final insertion rolls back the entire promotion transaction. The previously completed weekly data remains active.

Re-upload uses full weekly replacement rather than file-hash rejection or row-level upsert. The hash is retained for traceability only.

## Validation Rules

Batch validation includes:

- the file is a readable `.xlsx` workbook within the configured upload-size limit;
- the selected week-start is a Monday;
- exactly one active mapping matches;
- all eight required canonical values are present on every nonblank data row;
- numeric and date values can be converted without silent fallback;
- `InvoiceDate` is between the selected Monday, inclusive, and the following Monday, exclusive;
- values fit the lengths and numeric ranges supported by `SalesTransaction`;
- at least one nonblank data row exists.

Optional unmapped fields do not cause validation errors. Optional mapped fields that contain a nonblank invalid value do cause validation errors.

## User Interface

### Company mapping management

Edit Company gains a Sellout Template Mappings section with:

- mapping name;
- worksheet;
- header row;
- first data row;
- active/inactive status;
- create, edit, deactivate, and reactivate actions.

The mapping editor includes workbook preview, source-header selectors, canonical-field selectors, and visible required-field indicators. It uses the existing Companies Edit authorization policy and anti-forgery protection.

### Sellout upload

The Sales Transaction/Sellout upload interface requires:

- a Monday week-start picker;
- one `.xlsx` file.

There is no mapping dropdown. If the company has no active mapping, the interface blocks upload and directs the user to contact an administrator.

### Error display

A failed batch displays:

- batch-level errors;
- source row number;
- canonical field;
- rejected value;
- validation reason.

The page does not report success until promotion commits.

### Sellout browsing and downloads

The page provides both modes:

- **Week view:** rows for one selected Monday-through-Sunday reporting week.
- **Month view:** all current weekly batches whose invoice dates fall within the selected month.

Week and month downloads apply the same rules as their corresponding screen views. New weekly data is not filtered by the old global maximum `CreatedDate` rule. Legacy records continue using the prior latest-upload logic, except for weeks that have been replaced by an explicit mapped batch.

## Application Boundaries

The implementation follows the existing layered solution:

- Domain contains mapping, batch, staging, and entity relationships.
- Repository interfaces define mapping persistence, staging persistence, atomic promotion, and weekly/monthly reads.
- Repository implementations use the existing SQL Server/Dapper and EF Core conventions where appropriate.
- Services own header normalization, workbook inspection, detection, conversion, validation, and orchestration.
- MVC controllers remain thin and coordinate authorization, upload inputs, and views.

Workbook parsing and row validation are separate services so they can be tested without a database. Promotion is a repository operation with a clear transactional boundary.

## Database and Stored Procedure Changes

An EF Core migration creates the four new tables, relationships, constraints, and indexes and adds nullable batch/week metadata to `SalesTransaction`.

Existing `GiselSales_*` procedures are updated to support explicit week and month queries:

- week queries return the active explicit weekly batch, with a legacy fallback where no explicit batch exists;
- month queries combine all active explicit weekly rows in the month and only the appropriate latest legacy rows;
- downloads and paged queries share the same inclusion rules;
- replacement operates by company and week inside a transaction.

## Automated Verification

A new test project will cover:

- worksheet, header-row, and first-data-row configuration;
- header normalization and ordered signatures;
- required-field and duplicate-assignment validation;
- exact automatic detection;
- zero-match and multiple-match failures;
- blank-row handling;
- typed conversions for strings, integers, decimals, and dates;
- optional-unmapped and optional-invalid behavior;
- source-row and field-specific validation messages;
- selected-week invoice-date validation;
- complete failure without final-table changes when any row is invalid;
- successful weekly promotion;
- replacement of only the target company's target week;
- isolation of other companies and weeks;
- preservation of active data when promotion fails;
- combined monthly and exact weekly querying;
- legacy-row compatibility.

Verification also includes solution build, automated test execution, migration generation/inspection, and focused checks of the modified stored procedures.

## Acceptance Criteria

- An admin can save multiple valid named mappings for a company.
- A mapping cannot be saved without all eight core fields.
- An uploader does not select a mapping manually.
- An upload succeeds only when exactly one active company mapping matches.
- Every nonblank uploaded row is written to typed staging before promotion.
- One invalid row prevents all final sellout changes.
- A valid upload atomically replaces the same company's prior selected weekly dataset.
- Data for other companies and weeks is never removed by replacement.
- Failed rows show actionable source-row and field errors.
- Week view shows the selected active weekly batch.
- Month view combines all active weekly batches in the selected month.
- Existing legacy sellout history remains readable.
