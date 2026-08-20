# GiselX User Mapping and Administration Tutorial Design

**Date:** 2026-08-14  
**Deliverable:** English PDF user tutorial  
**Audience:** New GiselX administrators responsible for company mappings, roles, and users

## Objective

Create a polished, text-only PDF tutorial that teaches a GiselX administrator how to:

1. Configure a company-specific Sellout workbook mapping.
2. Configure a company-specific Stock workbook mapping.
3. Create or import distributor-to-GMK Item mappings.
4. Enable Item identity mapping for future Sellout and Stock uploads.
5. Create the exact `Administrator` role and assign the required permissions.
6. Create a user and assign the `Administrator` role.
7. Verify that the mappings and access configuration are ready for use.

The guide will use the labels and navigation found in the current GiselX application. It will not contain screenshots, passwords, secrets, or invented production records.

## Format and Visual Style

- PDF only, approximately 8-10 A4 pages.
- Clear cover page and table of contents.
- Numbered, task-oriented steps.
- Exact menu, page, field, and button labels shown in bold.
- Compact tables explaining mapping fields and recommended Administration permissions.
- Consistent callouts for prerequisites, tips, warnings, expected results, and verification.
- Page numbers, section headers, and a final completion checklist.
- English language matching the application's interface.

## Tutorial Structure

### 1. Introduction and prerequisites

Explain the tutorial's purpose, the required administrative access, and the need for representative `.xlsx` sample files. Explain that mappings are company-specific and that the administrator must select the correct company before configuration.

### 2. Open the company configuration

Direct the user to **Admin > Companies**, open the appropriate company, and identify these actions on **Edit Company**:

- **Manage Distributor Mappings**
- **Add Distributor Sellout Mapping**
- **Manage Stock Mappings**
- **Add Stock Mapping**
- **Manage Item Mappings**
- **Item identity mapping** checkbox

### 3. Configure the Sellout mapping

Explain how to open **Add Distributor Mapping**, enter a meaningful mapping name, upload a representative `.xlsx` sample, identify the worksheet and row numbers, select **Inspect sample**, map source headers to Sellout canonical fields, ignore irrelevant columns, save the mapping, and confirm that it is Active.

The tutorial will explain that the header row contains column names and the first data row contains the first transaction. It will also warn the user to map every required field once, avoid duplicate canonical assignments, and use a sample file that represents future uploads.

### 4. Configure the Stock mapping

Explain the corresponding workflow on **Add Stock Mapping**: mapping name, sample file, worksheet, header row, first data row, **Inspect sample**, Stock field assignments, **Save Mapping**, and Active status verification.

The tutorial will call out the importance of item identity fields, stock quantity, reporting date, and units such as price unit where present in the source workbook.

### 5. Configure Item mappings

Explain both supported paths:

- Manual creation with **Add Mapping** and the fields **GMK Item ID**, **GMK Item Name**, **Distributor Item ID**, and **Distributor Item Name**.
- Bulk import using **Download Template**, completing the workbook, uploading it on **Item Mappings**, selecting **Preview Import**, reviewing New/Updated/Unchanged/Invalid counts, and confirming the import.

Explain Active and Inactive states, search and sorting, and the rule that distributor item IDs must resolve to active GMK Item mappings when Item identity mapping is enabled.

### 6. Enable Item identity mapping

Direct the administrator back to **Edit Company**, select **Enable distributor-to-GMK item mapping for future Stock and Sales uploads**, save the company, and verify that all distributor item IDs expected in future files have active mappings before enabling the option.

### 7. Create the Administrator role

Direct the user to **Admin > Roles**, select **Create New Role**, enter the exact role name `Administrator` and a clear description, then create the role. The exact name is required because the current application uses it to enable administrator-wide data scope.

On **Assign Permissions to Role**, instruct the administrator to assign permissions required for the intended administration scope. For this tutorial, the recommended set includes all permissions in these groups:

- Companies
- Roles
- Users
- Sales Transactions
- Stocks

Optional groups such as Service Levels, Email Test, and Hangfire Dashboard will be explained as environment-dependent rather than mandatory.

### 8. Create the user

Direct the user to **Admin > Users**, select **Add New Record**, and complete **Username**, **Email**, **First Name**, **Last Name**, **Password**, **Confirm Password**, optional **Phone Number**, and **Company**. Explain that the selected company controls the user's company context and that the password must comply with the application's password policy.

### 9. Assign the Administrator role

From the Users table, direct the administrator to select the **Assign Roles** action, select **Administrator**, retain or clear other roles deliberately, select **Save Roles**, and verify that the role appears in the user's Role column.

### 10. Current Administrator data scope

Include the latest workspace behavior: a user in the exact `Administrator` role can see records across distributors on **Sales Transaction**, **Stock**, and **Service Level** pages. Administrator tables include a distributor/company column, Stock supports distributor filtering, completed Stock upload choices include the distributor, and administrator downloads include a leading **Distributor** column with filenames using `All_Distributors`. Non-administrator users remain limited to their assigned company or uploader scope.

### 11. Verification, troubleshooting, and final checklist

Provide checks for:

- Active Sellout mapping exists for the intended company.
- Active Stock mapping exists for the intended company.
- Required Item mappings exist and are Active.
- Item identity mapping is enabled only after coverage is complete.
- Administrator role contains the intended permissions.
- User is Active, belongs to the correct company, and displays the Administrator role.
- The user signs in again after role permission changes. Saving permissions refreshes security stamps for users in the role, so existing sessions may be invalidated.
- Administrator pages show the distributor/company context introduced by the latest workspace update.

Troubleshooting will cover missing menus due to permissions, invalid sample workbook structure, wrong worksheet or row numbers, unmapped required columns, duplicate or inactive Item mappings, failed bulk import validation, and a role that has been created but not assigned.

## Data and Security Rules

- Do not include or expose passwords, connection strings, tokens, or other secrets.
- Do not invent existing production users, companies, or mapping records.
- Use neutral examples only when explaining what a field means; clearly label them as examples.
- Describe operations using current GiselX labels without claiming that a record was created in the user's production database.
- Emphasize least privilege and deliberate selection of optional permissions.

## Acceptance Criteria

The final PDF is complete when:

1. Every requested workflow is covered in numbered steps.
2. Navigation and labels match the current source code.
3. Sellout, Stock, and Item mapping concepts are clearly distinguished.
4. Administrator role creation, permission assignment, user creation, and role assignment are each independently verifiable.
5. The PDF contains no screenshots and no sensitive credentials.
6. The rendered pages have no clipped text, overlap, broken tables, unreadable symbols, or inconsistent headers and footers.
7. Text extraction confirms that all expected sections are present.
8. The current Administrator cross-company behavior is explained, including distributor columns, Stock filtering, upload-history labels, and administrator export behavior.
