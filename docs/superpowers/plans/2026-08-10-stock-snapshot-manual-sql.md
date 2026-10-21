# Stock Snapshot Manual SQL Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a rerunnable SQL Server deployment script for the approved Stock snapshot-upload schema.

**Architecture:** A single transaction-scoped SQL script mirrors migration `20260810160000_AddStockSnapshotUploads`. It checks every table, column, constraint, and index before creating it, preserves existing Stock data, and rolls back automatically when any statement fails.

**Tech Stack:** SQL Server T-SQL, EF Core migration as schema authority, xUnit static contract tests.

## Global Constraints

- Do not update, delete, truncate, replace, or backfill any existing `dbo.Stock` row.
- Create only the four approved Stock mapping/audit tables and two nullable `dbo.Stock` columns.
- Use `SET XACT_ABORT ON`, `TRY/CATCH`, an explicit transaction, and `sp_getapplock`.
- Every schema object must have an existence guard so the script can be rerun.
- Match the EF migration's SQL types, lengths, nullability, defaults, foreign keys, checks, and indexes.

---

### Task 1: Idempotent Stock snapshot deployment script

**Files:**
- Create: `GiselX.Web/DatabaseScripts/20260810160000_AddStockSnapshotUploads.sql`
- Create: `GiselX.Tests/StockManualSqlScriptTests.cs`

**Interfaces:**
- Consumes: `GiselX.Web/Context/Migrations/20260810160000_AddStockSnapshotUploads.cs` as the canonical schema definition.
- Produces: a standalone SQL Server script runnable from SSMS against the target GiselX database.

- [ ] **Step 1: Write failing SQL contract tests**

Create `StockManualSqlScriptTests.cs` with a helper that walks upward from `AppContext.BaseDirectory` until it finds `GiselX.sln`, then reads the SQL file. Assert:

```csharp
[Fact]
public void Script_contains_transaction_and_rerun_guards()
{
    var sql = ReadScript();
    Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("OBJECT_ID(N'dbo.StockUploadBatch'", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("COL_LENGTH(N'dbo.Stock', N'StockDate')", sql, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void Script_creates_the_approved_schema_contract()
{
    var sql = ReadScript();
    foreach (var table in new[] { "StockTemplateMapping", "StockTemplateColumn", "StockUploadBatch", "StockStaging" })
        Assert.Contains($"CREATE TABLE dbo.{table}", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("StockUploadBatchId bigint NULL", sql, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("StockDate date NULL", sql, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void Script_has_no_destructive_stock_data_statement()
{
    var compact = Regex.Replace(ReadScript(), @"\s+", " ");
    Assert.DoesNotMatch(@"(?i)DELETE\s+(FROM\s+)?(?:dbo\.)?Stock\b", compact);
    Assert.DoesNotMatch(@"(?i)UPDATE\s+(?:dbo\.)?Stock\s+SET\b", compact);
    Assert.DoesNotMatch(@"(?i)TRUNCATE\s+TABLE\s+(?:dbo\.)?Stock\b", compact);
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\stock-sql-red\bin\ --filter FullyQualifiedName~StockManualSqlScriptTests --verbosity minimal
```

Expected: failure because `GiselX.Web/DatabaseScripts/20260810160000_AddStockSnapshotUploads.sql` does not exist.

- [ ] **Step 3: Create the deployment script**

Implement these statements in object-dependency order:

1. `SET NOCOUNT ON; SET XACT_ABORT ON;` and `USE` guidance as a comment, without hardcoding a database name.
2. `BEGIN TRY`, `BEGIN TRANSACTION`, and `sp_getapplock` with resource `GiselX_AddStockSnapshotUploads_20260810160000`, owner `Transaction`, mode `Exclusive`, and a 60-second timeout.
3. Throw when `dbo.Company` or `dbo.Stock` is missing.
4. Add nullable `dbo.Stock.StockUploadBatchId bigint` and `dbo.Stock.StockDate date` using `COL_LENGTH` guards.
5. Create the four tables with `OBJECT_ID(..., N'U') IS NULL` guards and the exact migration column definitions.
6. Add named default, check, primary/foreign-key constraints using `sys.key_constraints`, `sys.check_constraints`, `sys.default_constraints`, and `sys.foreign_keys` guards.
7. Add the migration indexes using `sys.indexes` guards.
8. Insert `20260810160000_AddStockSnapshotUploads` into `dbo.__EFMigrationsHistory` when absent, after all schema checks pass.
9. Commit and print `Stock snapshot upload schema installed successfully.`
10. In `CATCH`, roll back when `XACT_STATE() <> 0`, then `THROW`.

- [ ] **Step 4: Run targeted tests and verify GREEN**

Run the Step 2 command again.

Expected: all `StockManualSqlScriptTests` pass.

- [ ] **Step 5: Run final verification**

Run:

```powershell
dotnet test GiselX.Tests\GiselX.Tests.csproj --no-restore --nologo -p:BaseOutputPath=.tmp\stock-sql-final\bin\ --verbosity minimal
```

Expected: the complete test suite passes. Review the SQL to confirm dependency order and verify it contains no `DELETE`, `UPDATE`, or `TRUNCATE` operation against final Stock data.

- [ ] **Step 6: Commit if Git is available**

```powershell
git add GiselX.Web\DatabaseScripts\20260810160000_AddStockSnapshotUploads.sql GiselX.Tests\StockManualSqlScriptTests.cs docs\superpowers\specs\2026-08-10-stock-snapshot-manual-sql-design.md docs\superpowers\plans\2026-08-10-stock-snapshot-manual-sql.md
git commit -m "docs: add manual stock snapshot deployment script"
```

If Git remains unavailable, preserve the files in the current checkout and report that no commit was created.
