# Distributor Mapping Management Design

## Goal

Provide administrators with a dedicated per-company page where they can see and manage saved distributor sellout mappings.

## Scope

The feature adds a server-rendered mapping-management page linked from Company Edit. Administrators can open a mapping for editing, activate or deactivate it, and permanently delete it when it has never been referenced by an upload batch.

The feature does not delete upload batches, staging rows, or imported sales transactions. A mapping referenced by any upload batch cannot be deleted and must be deactivated instead.

## Routes and Navigation

- `GET /Admin/DistributorTemplateMappings?companyId={companyId}` displays all mappings for one company.
- Company Edit includes a **Manage Distributor Mappings** link to the management page.
- The management page includes **Add Mapping** and **Back to Company** links.
- `POST /Admin/DistributorTemplateMappings/SetActive` activates or deactivates one mapping.
- `POST /Admin/DistributorTemplateMappings/Delete` permanently deletes an unused mapping.
- Existing Create and Edit routes remain unchanged.

All mutation routes require an anti-forgery token and the existing `Companies.Edit` permission.

## Management Page

The page uses a strongly typed view model containing the company ID and the mapping list. Each row displays:

- mapping name;
- worksheet name;
- header row number;
- first data row number;
- mapped-column count;
- active or inactive status;
- created date and creator;
- modified date and modifier when available.

Each row provides these actions:

- **Edit** opens the existing mapping editor.
- **Activate** or **Deactivate** posts the inverse of the current status.
- **Delete** posts after a browser confirmation that describes the operation as permanent.

When no mappings exist, the page displays an empty-state message and an Add Mapping action.

## Existing Mapping Editor

Opening an existing mapping displays its stored mapped-column assignments instead of an empty table. The administrator can inspect a new sample workbook to replace the table and then save updated assignments. Saving an edit continues to require a sample workbook so the server can verify its worksheet and header signature.

## Application Flow

### List

1. The controller receives `companyId`.
2. The controller requests all mappings for that company from `IDistributorTemplateMappingService.ListAsync`.
3. The controller renders a typed management view.

### Activate or Deactivate

1. The browser posts mapping ID, company ID, and requested active state.
2. The controller calls `SetActiveAsync` with the authenticated username.
3. The service verifies that the mapping belongs to the supplied company.
4. The repository updates status and modification audit fields.
5. The controller redirects to the management page with a success or error message.

### Delete

1. The browser posts mapping ID and company ID after confirmation.
2. The service verifies that the mapping belongs to the supplied company.
3. The repository executes a guarded delete that succeeds only when no `SelloutUploadBatch` references the mapping.
4. Database cascade behavior removes the mapping's `DistributorTemplateColumn` rows.
5. If upload history exists, the service returns a clear error instructing the administrator to deactivate the mapping instead.
6. The service commits only after a successful delete.
7. The controller redirects to the management page with a success or error message.

The guarded database delete prevents a race between checking upload history and deleting the mapping.

## Error Handling and Security

- A mapping ID cannot be operated on through a different company ID.
- Missing mappings return a user-facing not-found error without committing a transaction.
- Protected mappings return an error explaining that upload history prevents permanent deletion.
- Mutation actions use POST and anti-forgery validation.
- Delete forms include a permanent-deletion confirmation.
- Repository deletion is constrained by both mapping ID and company ID.

## Interfaces

`IDistributorTemplateMappingService` gains:

```csharp
Task<ServiceResponse> DeleteAsync(
    int id,
    int companyId,
    CancellationToken cancellationToken);
```

`IDistributorTemplateMappingRepository` gains a guarded deletion operation:

```csharp
Task<DistributorTemplateMappingDeleteResult> DeleteAsync(
    int id,
    int companyId,
    CancellationToken cancellationToken);
```

The result distinguishes `Deleted`, `NotFound`, and `HasUploadHistory` so the service can return accurate messages without relying on database exception text.

## Testing

Automated tests cover:

- listing all mappings for the requested company;
- rendering or controller delivery of the mapping-management model;
- activating and deactivating a mapping;
- deleting an unused mapping and committing the transaction;
- blocking deletion when upload history exists without committing;
- rejecting a mapping/company mismatch;
- redirecting mutation results back to the correct company listing;
- displaying existing mapped assignments when opening Edit;
- preserving all existing distributor mapping and upload tests.

## Success Criteria

- An administrator can reach the management page from Company Edit.
- Every saved mapping for that company is visible with its status and audit information.
- Edit and Add navigation use the correct company ID.
- Activate and Deactivate update the mapping and return to the list.
- Delete permanently removes only unused mappings and their column assignments.
- Mappings referenced by upload history cannot be deleted.
- All existing and new automated tests pass.
