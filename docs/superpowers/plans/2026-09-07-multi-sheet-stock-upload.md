# Multi-Sheet Stock Upload — Implementation Plan

**Spec:** `docs/superpowers/specs/2026-09-07-multi-sheet-stock-upload-design.md`
**Tech Stack:** .NET 10, C# 14, ASP.NET Core MVC, EF Core 10 SQL Server migrations, Dapper, xUnit.
**Scope:** Stock upload pipeline only. The Sellout / SalesTransaction pipeline is untouched.

## Steps

- [x] **Step 1: Record worksheet provenance on staging rows**
  `StockStaging` and `StockStagingRowDto` gain `StockTemplateMappingId` and `SourceWorksheetName`;
  `StockStagingErrorDto` gains `WorksheetName`. EF config in `GiselXDbContext` maps the worksheet name
  at `nvarchar(255)` and adds the `StockTemplateMapping` FK with `Restrict`.
  Migration: `20260907024804_AddStockStagingSheetSource`.

- [x] **Step 2: Return every match from detection**
  `StockMappingDetectionResultDto` exposes `Matches` (mapping + resolved worksheet) and
  `IgnoredWorksheetNames`; `Mapping` becomes a computed convenience that is non-null only for a single
  match. `XlsxStockWorkbookInspector.DetectAsync` collects all matches, errors only when a worksheet is
  claimed by more than one mapping, and reports unmatched worksheets.

- [x] **Step 3: Analyse every matched worksheet**
  `StockWorkbookParser.Parse` accepts the resolved worksheet name.
  `StockWorkbookAnalysisService.AnalyzeAsync` loops the matches, tags rows and errors with their
  mapping and worksheet, resolves item mappings once per company, and fills
  `StockWorkbookAnalysisDto.Sheets` / `IgnoredWorksheetNames`.

- [x] **Step 4: Persist and report**
  `StockUploadService.ToStaging` forwards the provenance; `StockUploadRepository.InsertStagingAsync`
  writes the two new columns; `StockUploadPreflightDto` carries `Sheets` and `IgnoredWorksheetNames`,
  and `MappingName` lists every matched mapping with its worksheet.

- [x] **Step 5: Preflight and result UI**
  `Views/Stocks/Index.cshtml` gains a per-worksheet table, a "Skipped worksheets" row, and prefixes row
  errors with their worksheet. `Views/Stocks/Result.cshtml` shows a Worksheet column.

- [x] **Step 6: Manual SQL installer and tests**
  `GiselX.Web/DatabaseScripts/20260907024804_AddStockStagingSheetSource.sql` mirrors the migration.
  Tests: `MultiSheetStockDetectionTests`, new cases in `StockWorkbookAnalysisServiceTests`,
  `StockUploadServiceTests`, `StockPersistenceModelTests`, `StockManualSqlScriptTests`, plus
  `ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook`.

## Deployment

1. Databases managed by EF: startup `Database.MigrateAsync()` applies the migration.
2. Databases managed manually: run `20260907024804_AddStockStagingSheetSource.sql` once. It is
   idempotent, takes an applock, adds only nullable columns plus an index and FK, performs no DML, and
   records its own `__EFMigrationsHistory` row.
3. No backfill. Existing staging rows keep `NULL` provenance.

## Configuration for users

Nothing new to configure. A company that wants two worksheets uploaded together simply keeps both Stock
template mappings active, each pointing at its own worksheet. The upload page needs no extra input.
