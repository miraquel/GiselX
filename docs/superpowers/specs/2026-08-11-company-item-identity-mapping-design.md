# Company Item Identity Mapping

## Goal

Allow each distributor company to upload Stock and Sales Transaction workbooks using the distributor's own item IDs and names while storing a stable GMK item identity for GMK reporting. Distributor users must see their own item identity; GMK users must see the GMK identity.

## Scope

This feature adds one company-specific item crosswalk shared by Stock and Sales Transaction uploads. It includes manual administration, Excel bulk import, upload validation, audit preservation, and role-aware display and download behavior.

It applies only to future uploads. It does not remap historical Stock or Sales Transaction rows, change company-level data authorization, or create a separate global GMK item-master table.

## Chosen Approach

Resolve the distributor item identity during upload and store both identities on the resulting transaction row.

This keeps GMK reports stable if an administrator later edits a mapping and preserves the exact distributor identity that was uploaded. Resolving mappings only when reading data was rejected because it would make historical output change with the current mapping and would require every consumer to repeat mapping logic. Overwriting the uploaded identity without retaining it was rejected because it would remove audit traceability.

## Data Model

### Company

Add `ItemMappingEnabled`, a non-nullable boolean with a default of `false`.

- Existing companies remain mapping-disabled after migration.
- Mapping-disabled companies retain the current upload behavior.
- A company cannot enable item mapping unless it has at least one active item mapping.

### ItemMapping

Add one `ItemMapping` entity/table with:

- `Id`
- `CompanyId`
- `GmkItemId` (required, maximum 100 characters)
- `GmkItemName` (required, maximum 200 characters)
- `DistributorItemId` (required, maximum 100 characters)
- `DistributorItemName` (required, maximum 200 characters)
- `IsActive`
- `CreatedDate` and `CreatedBy`
- `ModifiedDate` and `ModifiedBy`

The table belongs to `Company` with restricted deletion. Distributor item ID is trimmed before persistence and is unique within a company using the database's case-insensitive comparison. Application validation must also compare trimmed IDs case-insensitively so duplicate errors are reported before the database constraint is reached.

One distributor item ID maps to exactly one GMK item within a company. Multiple distributor item IDs may map to the same GMK item. Distributor item name is required for complete administration data but is informational during transaction matching.

### Stock, Sales Transaction, and staging rows

Add these nullable fields to both final tables and their staging models:

- `DistributorProductId`
- `DistributorProductName`
- `ItemMappingId`

The existing `ProductId` and `ProductName` fields represent the GMK identity after a mapped upload. The distributor fields preserve the workbook values. `ItemMappingId` identifies the mapping used and remains nullable for old rows and mapping-disabled uploads.

For a mapping-enabled upload with an unmapped item, staging/analysis preserves the distributor fields, leaves the unresolved GMK identity empty, and attaches the row validation error. Final rows are created only after successful mapping according to the module's publication rules.

Mappings referenced by upload history or final rows cannot be deleted. Administrators deactivate them instead. Editing or deactivating a mapping never updates a previously uploaded transaction row.

## Mapping Administration

Only GMK administrators authorized with `Companies.Edit` may manage item mappings.

The Company Edit page provides:

- an `Enable Item Mapping` switch;
- a `Manage Item Mappings` link.

The item-mapping list provides:

- GMK Item ID;
- GMK Item Name;
- Distributor Item ID;
- Distributor Item Name;
- active status;
- search and sorting;
- add, edit, activate, deactivate, and delete commands;
- Excel template download and bulk import.

Activation and deactivation use separate POST actions with anti-forgery validation. The server owns the target state; it is not accepted from a posted boolean field.

All four item values are required for manual saves and Excel imports. Manual and bulk operations validate company ownership, length limits, trimmed values, and duplicate distributor IDs.

### Excel bulk import

The import template contains these required headers:

- `GMK Item ID`
- `GMK Item Name`
- `Distributor Item ID`
- `Distributor Item Name`

Import is a preview-and-confirm workflow. The preview reports new, updated, unchanged, and invalid row counts and gives row-specific validation messages. Duplicate distributor IDs within the workbook are invalid, including duplicates that differ only by casing or surrounding whitespace.

After confirmation, a valid workbook is applied transactionally:

- an existing company mapping with the same normalized Distributor Item ID is updated;
- a new Distributor Item ID is inserted as active;
- unchanged rows remain unchanged;
- mappings absent from the workbook remain unchanged.

No database changes are applied when the workbook contains invalid rows.

## Upload Resolution

Both workbook analysis services use the same item-mapping resolver after template-column parsing.

When `ItemMappingEnabled` is `false`:

- upload behavior remains compatible with the current system;
- uploaded Product ID and Product Name are copied to both the final product fields and distributor product fields;
- `ItemMappingId` is null.

When `ItemMappingEnabled` is `true`:

1. Trim the uploaded distributor Product ID and Product Name.
2. Find the active mapping for the current company using the distributor Product ID only, case-insensitively.
3. Preserve the uploaded values in `DistributorProductId` and `DistributorProductName`.
4. Copy `GmkItemId` and `GmkItemName` into `ProductId` and `ProductName`.
5. Store the mapping ID.

A difference between the uploaded Product Name and the mapping's Distributor Item Name is allowed and does not affect matching. The actual uploaded name remains available for display and audit.

If the distributor Product ID has no active mapping, add a row error such as:

> Row 24, ProductId: Distributor Item ID "P0003" has no active GMK item mapping.

Existing publication semantics remain unchanged:

- Stock may complete with warnings, publishing its valid rows and excluding unmapped rows.
- Sales Transaction rejects the upload group if any row is invalid, including an unmapped item.

Preflight and confirmed upload both perform resolution. The confirmed upload re-runs analysis so it cannot rely on stale preview results.

## Role-Aware Presentation

The existing Identity role is the source of the presentation mode:

- a user assigned the exact `Distributor` role sees Distributor Product ID and Distributor Product Name;
- every user without the `Distributor` role is treated as GMK and sees GMK Product ID and GMK Product Name;
- if a user has multiple roles and one is `Distributor`, distributor presentation wins.

This selection applies consistently to:

- Stock and Sales Transaction grids;
- API results used by those grids;
- Product ID and Product Name search, filtering, and sorting;
- Stock and Sales Transaction Excel downloads.

The outward DTO fields remain `ProductId` and `ProductName`; services/repositories populate them from the appropriate stored pair for the authenticated user's role. The UI therefore keeps one pair of item columns rather than exposing both identities.

For mapping-disabled uploads and migrated historical rows, both identities contain the same values, so either presentation mode gives compatible output.

Role-aware presentation does not broaden access. Existing company filtering and permissions continue to determine which records a user may view.

## Migration and Compatibility

The migration must:

- create `ItemMapping` and its indexes, constraints, and company relationship;
- add `Company.ItemMappingEnabled` with default `false`;
- add nullable distributor identity and mapping-reference columns to Stock, Sales Transaction, Stock staging, and Sellout staging;
- backfill existing final Stock and Sales Transaction distributor ID/name fields from their existing Product ID/name fields;
- leave historical `ItemMappingId` values null;
- update stored procedures and manual SQL artifacts that select, filter, sort, insert, or bulk-copy affected columns.

No migration attempts to infer or apply mappings to historical data. Development transaction tables may be truncated separately by the user after this feature is complete, but truncation is not part of the application migration.

## Error Handling

- Manual save conflicts return a field-level duplicate Distributor Item ID error.
- Excel errors identify the source row and field.
- Mapping enablement without an active row is rejected with a clear company-level message.
- Upload preflight exposes missing or inactive mappings as row validation errors.
- Database uniqueness and foreign-key constraints remain the final concurrency safeguard.
- Operations commit mapping changes, enablement changes, and confirmed imports transactionally.

## Testing

Automated coverage must include:

- company-specific, trimmed, case-insensitive Distributor Item ID matching;
- matching by ID without enforcing a Product Name match;
- missing and inactive mapping errors;
- duplicate manual mappings and duplicate Excel rows;
- Excel preview counts, confirmation, upsert behavior, and all-or-nothing invalid import behavior;
- rejection of enabling a company with no active mappings;
- Stock valid-row publication with unmapped rows excluded;
- Sales Transaction group rejection when any item is unmapped;
- mapping-disabled backward compatibility;
- preservation of both identities and the mapping reference;
- Distributor role display, search, sorting, and downloads;
- GMK display, search, sorting, and downloads for users without the Distributor role;
- multi-role Distributor precedence;
- company isolation and `Companies.Edit` authorization;
- migration model types, nullability, defaults, constraints, stored procedures, and manual SQL parity.

Focused tests should be followed by a complete solution build and full test suite.
