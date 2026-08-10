# Stock PriceUnit and Mapping State Design

## Objective

Extend the Stock snapshot upload flow with an optional per-row `PriceUnit` value and make Stock template mapping activation and deactivation deterministic.

The existing upload audit model remains unchanged in purpose:

- `StockUploadBatch` records upload-level metadata such as company, user, timestamps, file name/hash, status, and row counts.
- `StockStaging` records every parsed upload row, including invalid rows and their validation errors.
- `Stock` contains only valid published rows.

`PriceUnit` is row data. It belongs in `StockStaging` and `Stock`, not in `StockUploadBatch`.

## PriceUnit Data Contract

`PriceUnit` is nullable `decimal(18,2)` throughout the Stock pipeline.

- A blank source cell becomes `null` and does not invalidate the row.
- A numeric value greater than or equal to zero is accepted.
- A nonnumeric value produces a conversion error for `PriceUnit`.
- A negative value produces the validation error `PriceUnit must not be negative.`
- A row with an invalid `PriceUnit` remains in `StockStaging`, including its rejected value and validation error, but is excluded from the final `Stock` table.
- Other valid rows in the same upload are still published, and the batch completes with warnings under the existing partial-publication rules.

## Canonical Mapping

Add `PriceUnit` to the end of `StockCanonicalField`. Appending it preserves the existing integer values stored in `StockTemplateColumn.CanonicalField` for all current mappings.

`PriceUnit` is optional and therefore is not added to `StockCanonicalFieldCatalog.RequiredFields`. Administrators may map a distributor column to it when that company's workbook supplies a unit price. Existing mappings without `PriceUnit` remain valid and continue to upload `null` for this field.

The mapping editor automatically exposes the field through the existing enum-driven dropdown.

## Upload and Audit Data Flow

The value flows through these existing boundaries:

1. `StockWorkbookParser` converts a mapped source cell to nullable decimal and validates non-negativity.
2. `StockStagingRowDto` carries the parsed value during workbook analysis.
3. `StockUploadService` copies it into `StockStaging` for every uploaded row.
4. `StockUploadRepository` inserts it into `StockStaging` and publishes it to `Stock` only when `IsValid = 1`.
5. `StockDto` and the entity mapper return it for listing and downloads.

Both invalid and valid uploaded values therefore remain traceable through the existing upload batch and staging relationship.

This change does not add storage for the original `.xlsx` bytes. The current log continues to retain the original file name and SHA-256 hash only.

## Stock Screen and Downloads

Add a `Price Unit` column after `Unit` on the Stock table. The server-side list query allows `PriceUnit` as a sortable/filterable decimal field. Existing global text search behavior remains unchanged.

Batch downloads include `Price Unit` after `Unit`, and every later column shifts one position. Historical uploads created before this feature export a blank Price Unit value.

Any legacy Stock bulk-upload and stored-procedure projections that still participate in the application must also include `PriceUnit`, so every supported read/write path has the same contract.

## Mapping Activation and Deactivation

The current page posts a client-supplied `isActive` boolean to one `SetActive` action. Runtime behavior shows that this boundary always supplies `false`, because every click produces the server message `Mapping deactivated.`

Replace that toggle contract with two explicit POST commands:

- `Activate(id, companyId)` calls the existing mapping service with `isActive: true`.
- `Deactivate(id, companyId)` calls the existing mapping service with `isActive: false`.

The page renders only the applicable command for each row:

- An active mapping shows a `Deactivate` button posting to `Deactivate`.
- An inactive mapping shows an `Activate` button posting to `Activate`.

Neither form contains an `isActive` field. The server owns the requested state, eliminating boolean rendering and model-binding ambiguity.

The existing service and repository continue to:

- scope the update by both mapping ID and company ID;
- update `ModifiedBy` and `ModifiedDate`;
- commit the transaction only after a successful update;
- reject mappings that do not belong to the requested company.

Activating a mapping does not deactivate other mappings. The existing behavior allowing multiple active formats per company is preserved because different workbook templates can coexist.

Activating an already active mapping or deactivating an already inactive mapping is idempotent and returns the requested success message.

## Error Handling

- A missing or cross-company mapping returns the existing not-found response, displays an error after redirect, and does not commit.
- A database update affecting no row remains an error rather than reporting a false success.
- PriceUnit conversion and negative-value errors use the existing row-error format and appear in preflight and upload results.
- No new failure mode blocks valid rows merely because another row has an invalid PriceUnit.

## Database Deployment

Provide an EF migration and a matching manually runnable, transaction-protected SQL Server script.

The deployment must be safe when the user has already added `dbo.Stock.PriceUnit`:

- Add `dbo.Stock.PriceUnit decimal(18,2) NULL` only when it is missing.
- Add `dbo.StockStaging.PriceUnit decimal(18,2) NULL` only when it is missing.
- Verify that any existing columns have the expected decimal type, precision, scale, and nullability.
- Fail with a clear error when an existing column is incompatible instead of performing a potentially lossy automatic conversion.
- Record the migration in `dbo.__EFMigrationsHistory` only after all checks succeed.

No data backfill is required; historical rows remain `null`.

## Testing

Use test-driven implementation with these regression cases:

- The Stock `Activate` action forwards `true`, the company ID, mapping ID, and authenticated username.
- The Stock `Deactivate` action forwards `false` with the same ownership and audit inputs.
- The corresponding form for an active/inactive row targets the correct explicit action and does not submit `isActive`.
- The mapping service commits successful state changes and does not commit cross-company changes.
- Mapping validation accepts mappings that omit `PriceUnit`.
- The parser accepts blank and nonnegative PriceUnit values.
- The parser rejects nonnumeric and negative PriceUnit values while retaining row-level errors.
- Staging persistence and valid-row publication include `PriceUnit`.
- Stock listing and batch download include the field.
- The SQL deployment is idempotent and contains compatibility checks for both tables.

Run focused tests first, then the complete `GiselX.Tests` suite. Manual verification covers both button directions, an upload with mixed valid/invalid PriceUnit rows, the final Stock table, and a batch download.

## Out of Scope

- Enforcing `PriceUnit` as a required mapping or required row value.
- Automatically deactivating other mappings when one is activated.
- Changing the existing Stock snapshot replacement/display rules.
- Adding original workbook byte storage or a new admin history page.
