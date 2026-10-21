namespace GiselX.Tests;

public sealed class CleanupDuplicateStockSnapshotsScriptTests
{
    [Fact]
    public void Cleanup_ranks_batch_and_area_pairs_per_company_date_and_area()
    {
        var sql = ReadScript();

        Assert.Contains("SELECT DISTINCT s.StockUploadBatchId, s.StockAreaId", sql);
        Assert.Contains("PARTITION BY b.CompanyId, b.StockDate, COALESCE(d.StockAreaId,0)", sql);
        Assert.Contains("ORDER BY b.CompletedDate DESC, b.Id DESC", sql);
        Assert.DoesNotContain("PARTITION BY b.CompanyId, b.StockDate\r", sql);
        Assert.DoesNotContain("PARTITION BY b.CompanyId, b.StockDate\n", sql);
    }

    [Fact]
    public void Cleanup_deletes_rows_by_batch_and_area_key()
    {
        var sql = ReadScript();

        Assert.Contains("x.AreaKey = COALESCE(stock.StockAreaId,0)", sql);
    }

    [Fact]
    public void Cleanup_marks_a_batch_replaced_only_when_no_stock_rows_remain()
    {
        var sql = ReadScript();

        Assert.Contains("UPDATE  b SET Status = 4", sql);
        Assert.Contains("NOT EXISTS (SELECT 1 FROM dbo.Stock s WHERE s.StockUploadBatchId = b.Id)", sql);
    }

    private static string ReadScript()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "GiselX.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        var path = Path.Combine(current!.FullName, "GiselX.Web", "DatabaseScripts", "Cleanup_DuplicateStockSnapshots.sql");
        Assert.True(File.Exists(path), $"Expected SQL script at '{path}'.");
        return File.ReadAllText(path);
    }
}
