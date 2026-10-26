# Copy Mappings and Unmapped Item Follow-up Design

Status: draft
Date: 2026-10-26

## Summary

Three additions to the mapping administration screens, all aimed at the PT. GMK admin team:

1. **Copy mappings from another company.** On a target company, the admin picks a source company and
   copies its Sellout template mappings, Stock template mappings and Item mappings in one step.
2. **Map unmapped items in bulk.** The existing *Unmapped* tab on Item Mappings becomes an inline
   grid: the admin fills GMK Item ID / Name on many rows and saves them together, instead of opening
   the Create form once per item.
3. **One overview of unmapped items across companies.** A new admin page lists every company that still
   has unmapped distributor items, so the admin no longer has to open each company to find out.

## Why

A distributor with several locations is registered as one company per location (see CLAUDE.md,
"Multi-tenancy"). The locations almost always send the same workbook formats and use the same item
codes, yet today each location's mappings are built from scratch: upload a sample workbook, pick every
column, type the Good/Bad values, then enter or import every item mapping. That is repeated work and a
source of mistakes.

Unmapped-item discovery already exists (`ItemMappingRepository.GetUnmappedDistributorItemsAsync`,
derived from staging rows whose `ItemMappingId` stayed NULL, shown in the *Unmapped* tab). Two things
make it hard to act on: each item has to be mapped through a separate form, and nothing tells the admin
which companies have items waiting. Rows of an unmapped item are excluded from the published data, so
the delay is visible to the business.

## Goals

- Setting up a new location of an existing distributor takes one copy action plus review, not a full
  re-mapping.
- The admin can clear a company's unmapped list from one screen.
- The admin can see, on one page, which companies need item mapping work.

## Non-goals

- **No link between copies.** A copied mapping stands on its own. Editing the source later does not
  change the copy, and there is no "sync" or "linked template" concept.
- **No copying of history.** Upload batches, staging rows, published Stock/Sellout rows and Stock areas
  are never copied. Only mapping definitions are.
- **No overwriting on the target.** Copy only adds. Anything the target already has (a template with the
  same name, an item mapping with the same Distributor Item ID) is left untouched and reported as
  skipped.
- **No automatic republishing.** Rows excluded from earlier uploads because their item was unmapped stay
  excluded; the distributor still re-uploads after the items are mapped, as today. The screens say so.
- **No GMK guess from other companies.** Bulk mapping does not pre-fill a GMK item from another
  company's mapping of the same distributor code, because unrelated distributors can reuse codes for
  different products. (Copy covers the "same distributor" case deliberately and visibly.)
- **No email alert** for unmapped items in this round.

## Anti-goals (would make this a failure even if it works)

- A copy that silently overwrites or deletes anything the target company already had.
- A copied Stock template that the target can never use (e.g. target has Stock areas but the template
  has no Area column) being saved anyway.
- A copy that breaks uploads which work today, e.g. by adding a second active template for a format
  the target already reads.
- A partially-applied copy after an error — an exception rolls the whole copy back.

## Part 1 — Copy mappings from another company

### Entry point and flow

- Company Edit screen (`Areas/Admin/Views/Companies/Edit.cshtml`) gets a **Copy Mappings from Another
  Company** button next to the existing mapping buttons. It opens a new page for that target company.
- **Step 1 – choose source.** A dropdown of all other companies, shown as `Name – Location` (location
  omitted when blank). The target itself is not offered.
- **Step 2 – preview and select.** After a source is chosen, the page lists, in three sections:
  - *Sellout template mappings* of the source: one row each with name, worksheet, header/first-data
    row, column count, status, and a checkbox.
  - *Stock template mappings*: same shape.
  - *Item mappings*: a single checkbox "Copy item mappings" with counts — how many will be copied and
    how many will be skipped because the target already has that Distributor Item ID.
  Each template row shows the outcome it will have: **Will copy**, **Skipped – name already exists on
  this company**, **Skipped – this company already has an active template for this format**, or
  **Cannot copy – <reason>** (checkbox disabled). Active templates are checked by default; inactive
  ones are listed unchecked.
- **Step 3 – copy.** The admin clicks *Copy selected*. The result page/message states how many of each
  type were copied and lists anything skipped with its reason, then links back to the Company Edit
  screen.

### What is copied

- **Template mappings (Sellout and Stock):** `Name`, `WorksheetName`, `HeaderRowNumber`,
  `FirstDataRowNumber`, `HeaderSignature`, every column (`SourceColumnNumber`, `SourceHeader`,
  `CanonicalField`), and for Stock `GoodConditionValues` / `BadConditionValues`. The copy keeps the
  source's **active/inactive status**, with `CreatedBy` = the admin and `CreatedDate` = now;
  `ModifiedBy/Date` are empty.
- **Item mappings:** `GmkItemId`, `GmkItemName`, `DistributorItemId`, `DistributorItemName`, and
  `IsActive` as on the source (an inactive source mapping stays inactive on the target), with the
  admin as `CreatedBy`.

### Rules

- A template copy goes through the **same validation as saving that template by hand**
  (`StockTemplateMappingService.Validate` incl. the Stock-area rule, `DistributorTemplateMappingService.Validate`).
  The validation logic is shared, not duplicated.
  - Duplicate name on the target → *Skipped*.
  - **Competing format.** Uploads pick templates by header signature, not by name, and refuse a file
    that more than one active template matches (`XlsxSelloutWorkbookInspector` /
    `XlsxStockWorkbookInspector.DetectAsync`). So an *active* source template whose `HeaderSignature`
    (ordinal) equals that of an active template already on the target → *Skipped – this company already
    has an active template for this format*. For Stock the comparison is per worksheet name
    (case-insensitive), because Stock only refuses two active matches on the same sheet; for Sellout it
    is the signature alone. The same check runs among the selected copies themselves, so two active
    copies of one format cannot both land. Inactive copies are never skipped for this reason, since
    they take no part in matching.
  - Target has Stock areas and the template has no Area column → *Cannot copy* with the same message
    the save path gives today.
  - Any other validation failure (e.g. a legacy template missing a field that is required today) →
    *Cannot copy* with the validation messages.
- Item mapping duplicates are matched the way `ItemMappingService.Validate` matches them: trimmed,
  case-insensitive `DistributorItemId` within the target company.
- If the target has **Item mapping disabled** (`Company.ItemMappingEnabled = false`), item mappings can
  still be copied, but the preview shows a warning that they have no effect until item mapping is enabled
  on the company screen. Copy does not change the company setting.
- **Submit re-checks; errors roll back.** The preview is not a binding contract (no fingerprint
  round-trip). At submit time the server re-evaluates every selected item from current data and writes
  the ones still copyable inside the one ambient transaction, then commits once. Anything that became
  skipped/invalid since the preview (another admin added a same-named template, a Stock area was added)
  is not written and is listed on the result page with its reason. An exception during the write rolls
  everything back, so a copy is never left half-written by an error.
- **Permissions.** The page requires `Companies.Edit`. Each section additionally requires the target
  permission for that type: `DistributorTemplateMappings.Manage`, `StockTemplateMappings.Manage`,
  `ItemMappings.Manage`. A section the user lacks permission for is not shown and cannot be submitted.

## Part 2 — Bulk mapping on the Unmapped tab

- The *Unmapped* tab table (`Areas/Admin/Views/ItemMappings/Index.cshtml`) gains two input columns per
  row: **GMK Item ID** and **GMK Item Name**. **Distributor Item Name** becomes an editable input
  pre-filled with the discovered name (it is required on save and can be blank in the data).
- The existing per-row **Map** button stays for anyone who prefers the full form.
- **GMK item autocomplete.** The GMK Item ID input offers suggestions (HTML `datalist`) from the
  distinct `GmkItemId` / `GmkItemName` pairs already used in *any* company's item mappings — there is no
  GMK item master table. Picking an ID fills its name. Free typing is still allowed.
- One **Save mappings** button submits all rows. Rows with GMK Item ID and Name both blank are ignored.
  Rows that are filled are validated with the same rules as `ItemMappingService.Validate`; valid rows
  are inserted as active mappings in one commit, invalid rows are returned with their error next to the
  row and their typed values kept. (Partial success is intended here — one typo must not block forty
  correct rows.)
- After saving, the success message reminds the admin that earlier uploads containing these items were
  published without those rows and the distributor must re-upload them.
- Permission: `ItemMappings.Manage` (same as the Map button). Without it, the tab stays read-only as
  today.

## Part 3 — Unmapped items overview

- New admin page **Unmapped Items** (menu entry in `_VerticalMenu.cshtml`, gated on
  `ItemMappings.Index`).
- One row per company that has `ItemMappingEnabled = true` **and** at least one unmapped item:
  Company name, Location, number of unmapped items, total affected rows, last seen date. Sorted by last
  seen descending. Each row links to that company's Item Mappings page opened on the *Unmapped* tab.
- An empty state says every company with item mapping enabled is fully mapped.
- The query reuses the definition in `GetUnmappedDistributorItemsAsync` (same staging sources, same
  normalisation, same NOT EXISTS against `ItemMapping`), aggregated per company instead of per item, so
  the overview count always equals the count on the company's tab.

## Data and schema

No schema change and no migration. All three parts read and write existing tables
(`DistributorTemplateMapping`/`Column`, `StockTemplateMapping`/`Column`, `ItemMapping`, staging and
batch tables). New SQL is inline Dapper in the existing repositories, scoped by explicit `CompanyId`
parameters.

## Verification

The test suite can cover the service rules (what is copied/skipped/refused, validation reuse, bulk-save
partial success, permissions on sections). It cannot execute the SQL, so the new queries (overview
aggregate, GMK suggestion list, inserts) must be checked against a real SQL Server: copy between two
companies and inspect the rows, bulk-map a few unmapped items and confirm the overview and tab counts
drop together.

## Calls made by the designer (not discussed with the user)

- Copy lives on one page reached from Company Edit and handles all three types together, rather than a
  copy button on each of the three mapping screens.
- Duplicates on the target are skipped, never overwritten.
- Inactive source templates are listed but unchecked by default; every copy (template or item) keeps
  its source active/inactive status.
- An active template whose format the target already reads with an active template is skipped, so a
  copy can never make an upload ambiguous.
- Submit re-checks against current data and writes what is still valid, reporting the rest, instead of
  refusing the whole copy when something changed since the preview.
- Bulk mapping accepts partial success; copy is all-or-nothing.
- GMK suggestions come from existing item mappings of all companies.
- Admin screens stay in English, matching the existing mapping screens.
