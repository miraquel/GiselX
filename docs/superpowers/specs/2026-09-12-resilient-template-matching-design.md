# Resilient Template Matching Design

**Date:** 2026-09-12
**Status:** implemented (phase one). Phase two — header aliases — is specified below but not built.

## Summary

A distributor workbook is matched to a template mapping by comparing the entire header row, normalized and
joined, against the signature stored on the mapping. The comparison is ordinal and total: one added column,
one reordered pair, or one renamed header rejects the workbook outright, and the distributor cannot fix it
themselves because creating mappings is an administrator's screen.

This design keeps that exact match as the first and preferred rule, and adds a second rule that runs only
when no mapping matched exactly: resolve each mapped column by its own header text rather than by its
position. A workbook whose columns moved, or which gained a column the mapping never referenced, then
still uploads. A workbook that matches today keeps matching through the unchanged first rule, so no
currently working upload changes behaviour.

Header-based resolution needs no schema change. `DistributorTemplateColumn` and `StockTemplateColumn`
already store `SourceHeader` beside `SourceColumnNumber`; only `SourceColumnNumber` is used at read time
today, and `SourceHeader` is the column identity this design starts trusting.

## Goals

- Accept a workbook that gained or lost a column the mapping never mapped.
- Accept a workbook whose columns were reordered.
- Leave every workbook that matches today matching by the same rule, with the same result.
- Report a column that genuinely cannot be found by naming the header the template expects.
- Keep one matching implementation shared by the Stock and Sellout pipelines.
- Require no database migration.

## Non-Goals

- Accepting a **renamed** mapped header. That needs alias data the system does not hold; it is phase two
  below and is not implemented by this design.
- Guessing a canonical field from an unrecognised header. Mapping stays an explicit administrator action.
- Changing how rows are validated, staged, or published once a mapping has been chosen.
- Changing the mapping editor UI.

## Matching rules

Detection runs the two rules in order and stops at the first that yields exactly one mapping.

### Rule 1 — exact signature (unchanged)

The normalized header row equals the mapping's `HeaderSignature` ordinally. This is today's rule, kept
verbatim, and it remains the only rule that can succeed for a workbook that already uploads cleanly.

### Rule 2 — resolve each mapped column by its header

Applied only when rule 1 matched no mapping. For each column on the mapping, find the workbook column whose
normalized header equals the column's normalized `SourceHeader`:

| Situation | Outcome |
| --- | --- |
| Exactly one workbook column carries that header | that column number is used, whatever its position |
| No workbook column carries that header | the mapping does not match; the rejection names the header |
| Several carry it | cannot occur — `WorkbookHeaderNormalizer.ValidateHeaders` already rejects a worksheet with duplicate normalized headers before detection sees it |

The mapping matches when **every** mapped column resolves. Workbook columns no mapping column claims are
ignored rather than rejected, which is what makes an added column harmless.

### Resolved column numbers replace stored positions

Detection returns, alongside the chosen mapping, the column number each canonical field resolved to. The
parser reads cells through that map instead of through `SourceColumnNumber`. Under rule 1 the resolved map
is identical to the stored positions, so the parser's behaviour is unchanged for today's workbooks.

## Ambiguity

Rule 2 is more permissive than rule 1, so more than one mapping can satisfy it where previously the exact
signature separated them. The existing multi-match errors are kept and apply unchanged:

- Sellout: "Multiple active distributor mappings exactly match this workbook: … Deactivate all but one."
- Stock: reported per contested worksheet, since one Stock workbook may legitimately feed several mapped
  worksheets and only two mappings claiming *one* worksheet is ambiguous.

Because rule 2 runs only when rule 1 found nothing, an ambiguity it introduces can never displace a mapping
that would have matched exactly.

## Rejection messages

`XlsxWorkbookInspector.DescribeRejection` already explains a rule 1 failure by column number and expected
text. It gains a rule 2 branch: when a mapped header was not found anywhere in the workbook, the rejection
names the missing header and the canonical field it feeds, because that is the thing an administrator has
to fix in the mapping — or the distributor in the file.

## Phase two — header aliases (not in this design)

A renamed mapped header still fails. Supporting it means one canonical field accepting several spellings,
which is stored data and therefore a migration: a `SourceHeaderAliases` column on the template column, or a
child table. It is deliberately separated because phase one needs no migration and can ship first.

## Automated Verification

- A workbook with an extra unmapped column, rejected today, matches under rule 2.
- A workbook whose two mapped columns are swapped matches, and rows read from the right cells.
- A workbook missing a mapped header does not match, and the rejection names that header.
- A workbook matching a mapping exactly still matches through rule 1, and rule 2 is never consulted.
- Two mappings that both satisfy rule 2 for one worksheet report the existing ambiguity error.
- Rule 2 resolution is exercised through both the Stock and the Sellout inspector.

## Acceptance Criteria

- No database migration accompanies this change.
- Every existing test passes unchanged.
- Detection and parsing share one column-resolution implementation across both pipelines.
