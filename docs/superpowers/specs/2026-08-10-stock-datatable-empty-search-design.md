# Stock DataTable Empty Search Fix Design

## Problem

The Stock DataTable sends `SearchTerm=` when no search text is entered. ASP.NET Core converts the empty query-string value to `null`; automatic API validation then rejects the non-nullable `SearchTerm` property and returns HTTP 400 before the Stock API action executes.

## Approved behavior

- The Stock table must load successfully when its search box is empty.
- A non-empty global search must continue to be sent as `SearchTerm`.
- Empty search values from other API clients must also be accepted.
- Stock upload, mapping, publication, and database schema behavior must remain unchanged.

## Design

Use two small, complementary guards:

1. In the Stock DataTable request builder, add `SearchTerm` only when DataTables provides a non-empty value.
2. Mark `SearchTerm` nullable in the DTO and domain request models so ASP.NET Core does not infer a required-value validation rule and downstream mapping remains nullability-consistent.

The repository already uses `string.IsNullOrWhiteSpace`, so both `null` and an empty value safely mean “no global search.”

## Verification

- Add a regression test using ASP.NET Core MVC validation services to prove a null search term is accepted.
- Run that test before implementation and confirm it fails because `SearchTerm` is currently implicitly required.
- Apply the DTO/domain and Stock view changes.
- Run the targeted test and then the complete test suite.
- Confirm the generated Stock AJAX request no longer includes `SearchTerm` when the search box is empty.
