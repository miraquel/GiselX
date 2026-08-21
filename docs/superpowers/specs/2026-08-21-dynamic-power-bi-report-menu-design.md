# Dynamic Company-Scoped Power BI Report Menu Design

**Date:** 2026-08-21
**Status:** Approved in chat; awaiting written-spec review

## Summary

GiselX will let an administrator configure any number of public Power BI reports without redeploying the application. Each configuration belongs to exactly one company and becomes a standalone sidebar menu item for signed-in users of that company. The administrator supplies the menu/page name and a Power BI Publish-to-web URL. Selecting the menu item opens a responsive embedded report viewer inside GiselX.

There is no report catalog and no shared Power BI parent menu. Each active report appears directly in the main navigation under its administrator-defined name.

## Goals

- Allow administrators to create, edit, order, activate/deactivate, preview, and delete report configurations at runtime.
- Assign every report configuration to exactly one company.
- Show company users only active reports assigned to their own company.
- Render each configured report as a standalone sidebar item using the configured name.
- Open reports in an in-app, full-width responsive viewer.
- Enforce company isolation on the server for both menu generation and report viewing.
- Follow the existing Domain, Repository, Service, DTO, Mapper, MVC, Entity Framework, Identity, and test patterns in GiselX.

## Non-goals

- Creating, editing, refreshing, deleting, or unpublishing reports in Power BI.
- Secure Power BI embedding, Microsoft Entra authentication, embed tokens, or row-level security.
- Accepting or storing arbitrary iframe HTML.
- Uploading custom report thumbnails or icons.
- Providing a report catalog, search page, or nested Power BI navigation group.
- Assigning one report configuration to multiple companies.
- Giving company or distributor accounts any report-management capability.

## Public-data constraint

This feature uses Power BI **Publish to web** URLs. GiselX authentication controls which configured link appears in its UI, but it does not make the Power BI report private. Anyone who obtains the public URL can open the report without GiselX or Power BI authentication. Publish to web can also expose detail-level data underlying the report, and it does not support reports secured with row-level security.

Administrators must configure only reports whose data is approved for public Internet access. The create and edit forms will display this warning next to the URL field. Moving to secure Power BI embedding in the future would be a separate feature.

Reference: [Microsoft Power BI Publish to web documentation](https://learn.microsoft.com/en-us/power-bi/collaborate-share/service-publish-to-web)

## Domain model

Add a `PowerBiReport` entity with these properties:

| Property | Type | Rules |
| --- | --- | --- |
| `Id` | `int` | Primary key |
| `CompanyId` | `int` | Required foreign key to `Company` |
| `Name` | `string` | Required; trimmed; maximum 150 characters; used for the sidebar label and viewer heading |
| `EmbedUrl` | `string` | Required; maximum 2,048 characters; validated Publish-to-web HTTPS URL |
| `DisplayOrder` | `int` | Required; non-negative; lower values appear first |
| `IsActive` | `bool` | Defaults to `true`; inactive reports are hidden and cannot be opened from the normal viewer |
| `CreatedDate` | `DateTime` | UTC creation timestamp |
| `CreatedBy` | `string` | Required administrator identity; maximum 256 characters |
| `ModifiedDate` | `DateTime?` | UTC timestamp of the most recent edit |
| `ModifiedBy` | `string?` | Most recent administrator identity; maximum 256 characters |

`Company` gains a `PowerBiReports` navigation collection. The relationship is one company to many reports. Deleting a company remains restricted while report rows reference it, consistent with existing company-owned data.

The database enforces a unique company/name constraint so two reports for the same company cannot produce indistinguishable menu items. Name comparison follows the database collation. An index on `(CompanyId, IsActive, DisplayOrder, Name)` supports menu reads.

Deleting a report configuration is a hard delete after explicit administrator confirmation. Deactivation is the reversible alternative. Neither operation changes anything in Power BI.

## Application components

### Repository

Add `IPowerBiReportRepository` and `PowerBiReportRepository` with focused operations:

- List all configurations for administration, optionally filtered by company.
- List active configurations for one company in menu order.
- Get a configuration by ID for administration.
- Get an active configuration by both ID and company ID for the normal viewer.
- Detect a duplicate name within a company, excluding the current record during edit.
- Create, update, set active state, and delete a configuration.

Normal-user repository methods always require an explicit company ID and include it in the query predicate. They do not expose an unscoped lookup for the viewer.

### Service

Add `IPowerBiReportService` and `PowerBiReportService` to:

- Resolve and validate normalized form data.
- Validate Power BI URLs through a dedicated URL validator.
- Enforce duplicate-name rules.
- Assign audit values from the current administrator.
- Provide administrator management operations.
- Provide company-scoped active menu items and viewer details.

The current user's company is resolved server-side from the authenticated Identity user. A normal request never selects or posts its own company scope.

### DTOs and mapping

Use separate models for clear trust boundaries:

- An administrator list/details DTO containing company and audit information.
- A create/edit input model containing `CompanyId`, `Name`, `EmbedUrl`, `DisplayOrder`, and `IsActive`.
- A menu item DTO containing only `Id` and `Name`.
- A viewer DTO containing only the report ID, name, and validated embed URL.

Add mappings using the repository's existing Mapperly approach where it reduces manual mapping without weakening the input/output boundaries.

### Registration and persistence

- Register the repository and service in their existing `ServiceCollectionExtensions` classes.
- Add the entity to `GiselXDbContext` and configure field lengths, relationship behavior, uniqueness, defaults, and indexes.
- Add an Entity Framework migration and a matching manual SQL script if that is the established deployment pattern for this database change.

## Authorization and tenant isolation

### Company/distributor users

- The dynamic menu and viewer require an authenticated user.
- Menu data is loaded from the authenticated user's `CompanyId` and includes active rows only.
- The viewer loads by both route ID and authenticated user's `CompanyId` and includes active rows only.
- A report that is missing, inactive, or assigned to another company returns `404 Not Found`. The same response prevents callers from learning whether another company's ID exists.
- No create, edit, state-change, reorder, or delete endpoint is available to normal users.

### Administrators

- The complete management controller is protected with the hard `Administrator` role requirement.
- Every GET and POST management action remains behind that controller-level rule.
- Administrator POST actions use antiforgery validation.
- Company assignment is accepted only within these administrator-only operations.
- An administrator can preview a configured public report from the management UI, regardless of the administrator account's own company assignment.

The application does not depend solely on hiding UI controls. Server-side authorization and company predicates are mandatory.

## Dynamic sidebar behavior

Replace direct data access in Razor with a focused menu view component or equivalent presentation component. On each full-page render it will:

1. Read the current authenticated user identity.
2. Resolve the user's company on the server.
3. Request active report menu items for that company.
4. Render one standalone sidebar item per result, ordered by `DisplayOrder` and then `Name`.

Each item uses a common Power BI/report icon, its configured `Name`, and a route to `/PowerBiReports/View/{id}`. The item receives the normal active-navigation styling while its viewer is open. If a company has no active reports, no report-specific menu item or placeholder is rendered.

The existing static navigation remains unchanged apart from inserting the dynamic report items in the agreed main-menu location. Reports are not placed under a parent menu.

## Administrator experience

Add **Manage Power BI Reports** to the Admin navigation. The index page displays a table containing:

- Company
- Menu/page name
- Display order
- Active state
- Created/modified information where useful
- Preview, edit, activate/deactivate, and delete actions

The index supports a company filter so administrators can manage a large report set. Default ordering is company name, then display order, then report name.

Create and edit forms contain:

- Required company selector
- Required menu/page name
- Required Power BI Publish-to-web URL
- Required non-negative display order
- Active checkbox, enabled by default on create

For a new record, the form defaults `DisplayOrder` to the next available value for the chosen company when practical. Saving changes is immediately reflected on subsequent page loads; no application deployment or settings-file change is required.

Delete requires a confirmation page or modal naming both the report and company. Deactivate remains available as the preferred reversible way to remove a menu item temporarily.

## Viewer experience

`/PowerBiReports/View/{id}` renders:

- The configured report name as the page title and heading.
- A responsive, full-width iframe sized to make the report usable within the existing application layout.
- A loading treatment while the frame initializes.
- An **Open full screen** action, implemented with the browser Fullscreen API when available.
- A fallback link that opens the same public report URL in a new tab.

The server supplies only a previously validated and encoded URL. The view never renders administrator-provided HTML. Because Power BI is cross-origin, GiselX will not claim to inspect Power BI's internal load or rendering errors. The fallback remains available when browser policy, connectivity, Power BI status, or an invalidated publish code prevents the iframe from working.

## URL validation and browser security

The URL validator will:

- Parse with `Uri.TryCreate` as an absolute URI.
- Require the `https` scheme.
- Require the host to equal `app.powerbi.com`, using an ordinal case-insensitive comparison. Suffix or substring matches are not accepted.
- Require the public viewer path `/view`, allowing a trailing slash.
- Require a non-empty `r` query parameter used by Publish to web.
- Reject embedded credentials, URL fragments, control characters, and malformed input.
- Store a normalized URL rather than raw surrounding whitespace.

The Razor view uses normal attribute encoding. If GiselX has or introduces a Content Security Policy, its `frame-src` directive must explicitly allow `https://app.powerbi.com`; no wildcard frame source is required.

The validator intentionally accepts only the agreed Publish-to-web link format. Other Power BI share links, arbitrary websites, and raw iframe snippets produce a field-level validation error.

## Error handling

- Invalid administrator input returns the form with field-level errors and preserves entered values.
- A duplicate company/name combination returns a clear validation error without relying on a raw database exception.
- A database uniqueness race is translated into the same friendly duplicate-name result.
- A missing company selection is rejected before persistence.
- A missing, inactive, or other-company normal viewer request returns `404`.
- An unauthenticated viewer request follows the application's normal sign-in challenge.
- An unauthorized management request returns the framework's normal forbidden/challenge response.
- If the public Power BI code is later revoked or blocked, the GiselX record remains editable; the viewer offers retry and open-new-tab actions, and the administrator can replace or deactivate the URL.

## Testing strategy

### Unit and service tests

- Valid Publish-to-web URL acceptance and normalization.
- Rejection of HTTP, lookalike hosts, unsupported paths, missing report token, fragments, credentials, control characters, raw HTML, and malformed URLs.
- Required fields, length limits, and non-negative display order.
- Duplicate report names within one company are rejected.
- The same report name in different companies is allowed.
- Active menu items are filtered by company and ordered correctly.
- Inactive rows never appear in the normal menu or viewer.
- Audit fields are assigned on create and update.

### Repository and persistence tests

- Entity relationships, unique constraint, indexes, lengths, and delete restriction are represented in the EF model and migration.
- Company-scoped lookups include both report ID and company ID.
- Administrator list filtering and mutation operations work as designed.

### Controller and presentation tests

- A company user can open an active report belonging to that company.
- A company user receives `404` for another company's report, an inactive report, or an unknown ID.
- Unauthenticated access is challenged.
- Every administrator mutation is rejected for a non-administrator.
- Valid administrator create, edit, state change, and delete flows invoke the intended service operations.
- Invalid administrator input redisplays the form safely.
- The menu component renders only the current company's active items in configured order.
- Menu labels are encoded and cannot inject markup.
- The viewer emits an encoded Power BI iframe URL and the expected fallback/fullscreen controls.

### Verification commands

- Build the full solution.
- Run the complete automated test suite.
- Apply or validate the migration against the supported development database setup.
- Manually verify two companies with distinct report sets, an inactive report, dynamic menu ordering, viewer responsiveness, full screen, and administrator CRUD behavior.

## Rollout and operational notes

1. Apply the database migration before using the management page.
2. Deploy the application changes.
3. Have an administrator add public-safe Power BI URLs and assign each to its company.
4. Test with a non-administrator account from each configured company.
5. Review Publish-to-web codes periodically in Power BI and deactivate GiselX configurations that are no longer needed.

Existing users and companies require no data backfill. With no report configurations, the application behaves as before and renders no dynamic report menu items.

## Acceptance criteria

- An administrator can add a named Power BI Publish-to-web URL for one company without redeploying GiselX.
- The new active configuration appears as its own top-level sidebar item for users of that company.
- It does not appear for users of any other company.
- Clicking the item opens the corresponding report in an in-app responsive viewer.
- A user cannot open another company's configured report by changing the route ID.
- Inactive reports are absent from navigation and inaccessible through the normal viewer.
- Only users in the `Administrator` role can create, edit, reorder, activate/deactivate, preview across companies, or delete configurations.
- The configured UI name controls both the sidebar label and viewer page heading.
- Invalid or non-Power-BI URLs and raw iframe HTML are rejected.
- Adding, renaming, reordering, activating, or deactivating a report is reflected without an application redeployment.
