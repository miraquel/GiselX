# Sales Transaction Mapping Activation Repair

## Goal

Make Sales Transaction mapping activation and deactivation deterministic, matching the working Stock mapping menu.

## Root Cause

The Sales Transaction mapping list posts to a single `SetActive` action and supplies the desired state through a hidden `isActive` form value. This leaves the state transition dependent on browser-supplied boolean binding. The Stock mapping menu avoids that boundary by using separate server-owned commands.

## Design

- Replace the public `SetActive(int id, int companyId, bool isActive, ...)` MVC action with two POST actions:
  - `Activate(int id, int companyId, ...)`, which always forwards `true`.
  - `Deactivate(int id, int companyId, ...)`, which always forwards `false`.
- Keep anti-forgery validation and the existing company-edit authorization.
- Render a Deactivate form only for active rows and an Activate form only for inactive rows.
- Remove every Sales Transaction mapping form field named `isActive`.
- Continue forwarding the authenticated username for audit history.
- Keep the existing service and repository implementation because they already validate mapping ownership by company, update the requested state and audit fields, and commit transactionally.
- Activating one mapping does not deactivate other mappings.

## Error Handling

Both commands continue using the existing `RedirectToList` response handling. Successful service responses appear in `StatusMessage`; failed responses appear in `ErrorMessage`.

## Testing

- Replace the controller's direct `SetActive` test with separate Activate and Deactivate tests.
- Verify each action forwards the correct constant together with the mapping ID, company ID, and authenticated username.
- Verify redirect routing and status messages.
- Compile the web project so Razor tag helpers and action names are validated.
- Run the complete test suite after the focused regression tests pass.

## Scope

This repair changes only the Sales Transaction mapping command boundary and list forms. It does not change mapping persistence, upload selection, activation exclusivity, or unrelated Sales Transaction behavior.
