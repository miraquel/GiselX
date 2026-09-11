# Stock Snapshot Manual SQL Deployment Design

## Objective

Provide a SQL Server script that manually installs the approved Stock snapshot-upload schema represented by migration `20260810160000_AddStockSnapshotUploads`.

## Deployment behavior

The script will be idempotent and safe to rerun. It will:

- run with `XACT_ABORT ON` inside an explicit transaction;
- acquire a transaction-scoped application lock so two deployments cannot run concurrently;
- verify that the existing `dbo.Company`, `dbo.Stock`, and EF migration-history tables are available;
- add nullable `StockUploadBatchId` and `StockDate` columns to `dbo.Stock` only when absent;
- create `dbo.StockTemplateMapping`, `dbo.StockTemplateColumn`, `dbo.StockUploadBatch`, and `dbo.StockStaging` only when absent;
- create missing constraints and indexes individually;
- preserve all existing Stock rows without updating, deleting, or backfilling them;
- record `20260810160000_AddStockSnapshotUploads` in EF migration history only after the schema is valid, so application startup does not try to apply the migration again;
- commit on success and roll back on any error;
- print a clear completion message.

## Schema fidelity

Column types, lengths, nullability, defaults, status representation, check constraints, delete behaviors, and index definitions will match the application EF model and migration. Foreign keys will use the expected actions:

- Company to mapping: no action/restrict;
- mapping to columns: cascade;
- Company to upload batch: no action/restrict;
- mapping to upload batch: no action/restrict;
- upload batch to staging: cascade;
- upload batch to final Stock: no action/restrict.

## Partial-install handling

For an existing table, the script will verify required columns before adding dependent keys or indexes. If an existing object has an incompatible definition, SQL Server will surface an error and the transaction will roll back instead of silently replacing it.

## Verification

After generation, the script will be checked for:

- valid SQL object-creation order;
- no destructive DML against existing Stock data;
- all four audit tables and two nullable Stock columns;
- all required primary keys, foreign keys, defaults, checks, and indexes;
- compatibility with repeated execution.
- compatibility with the application's startup call to `Database.MigrateAsync()`.
