# Workbook Date Parsing Design

Status: approved
Date: 2026-09-07

## Summary

Date cells in distributor workbooks are parsed by one shared parser, `WorkbookDateTextParser`, used by
both `SelloutCellValueConverter` and `StockCellValueConverter`. It accepts Excel date serials, Indonesian
and English month names — including local abbreviations no culture data carries — numeric day-first and
ISO forms, two-digit years, and the non-breaking and thin spaces report exporters emit.

The parser replaces a `DateTime.TryParse` against `InvariantCulture` then `id-ID`. That pair rejected an
entire month of sellout: in `RAW DATA PT GMK PERIODE AGUSTUS 2026.xlsx` every `Tanggal Faktur` cell of the
form "21 Agu 2026" failed, leaving one English-formatted row ("27-Aug-26") valid out of 245. Because the
Sellout pipeline is all-or-nothing, the upload could not proceed at all.

## Accepted input

| Shape | Example |
| --- | --- |
| OLE Automation serial (a real Excel date cell) | `46261` |
| Indonesian abbreviated / full month | `21 Agu 2026`, `19 Agustus 2026` |
| Local abbreviations outside CLDR | `21 Agt 2026`, `21 Agust 2026`, `3 Nop 2026`, `28 Peb 2026`, `5 Mrt 2026` |
| English abbreviated / full month | `27-Aug-26`, `27 September 2026` |
| Day-first numeric, any of ` `, `-`, `/`, `.` | `27/08/2026`, `27-08-2026`, `27.08.2026` |
| Year-first numeric | `2026-08-27` |
| Two-digit year (00-49 → 2000s, 50-99 → 1900s) | `31/12/99` → 1999-12-31 |
| Any of the above with a trailing time | `27/08/2026 00:00:00` |

Month names are matched from an explicit table, not from culture data, so results do not shift with the
host's ICU version. Whitespace variants — U+00A0, U+2007, U+2009, U+202F — are normalized to ordinary
spaces, and zero-width characters (U+200B-U+200D, U+FEFF) are dropped, before anything is parsed.

Ambiguous numeric dates are read day-first, as Indonesian workbooks are written, and only fall back to
month-first when the day-first reading is not a real date: `08/27/2026` still reads as 27 August 2026.

## Rejected input

A date with no year — `21 Agu`, `21/08` — is rejected rather than completed with the current year. The
old culture-based parse accepted these silently, which would file an invoice into whichever reporting week
the upload happened to run in. Everything else that is not one of the shapes above is rejected as before,
and a dash placeholder (`-`, `–`, `—`) still means "empty".

## Error reporting

A rejected cell now reports the text it held: `Row 2, InvoiceDate: Value is not a valid DateTime (cell
value: "…")`, in the Sellout preflight error list (`SelloutUploadService.CollectErrors`) and in the Stock
preflight review. Without the value, "not a valid DateTime" gives the user nothing to act on.

## Non-Goals

- No change to which canonical fields are dates, or to whether they are required.
- No change to the Sellout all-or-nothing rule: any invalid row still blocks the whole upload.
- No inference of a missing year from neighbouring rows or from the workbook's file name.

## Automated Verification

`WorkbookDateTextParserTests` covers every accepted shape in the table above, each exporter whitespace
variant, the year-less rejections, the month-first fallback, and that both converters resolve their date
fields through the parser.
