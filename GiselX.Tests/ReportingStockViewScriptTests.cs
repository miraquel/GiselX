namespace GiselX.Tests;

public sealed class ReportingStockViewScriptTests
{
    [Fact]
    public void Stock_view_ranks_live_batches_per_company_date_and_area()
    {
        var sql = ReadScript();

        Assert.Contains("PARTITION BY b.CompanyId, b.StockDate, COALESCE(d.StockAreaId,0)", sql);
        Assert.Contains("ORDER BY b.CompletedDate DESC, b.Id DESC", sql);
        Assert.Contains(
            "live.Id = stock.StockUploadBatchId AND live.AreaKey = COALESCE(stock.StockAreaId,0) AND live.LiveRank = 1",
            sql);
    }

    [Fact]
    public void Stock_view_exposes_area_name_and_condition_label()
    {
        var sql = ReadScript();

        Assert.Contains("area.Name", sql);
        Assert.Contains("AS AreaName", sql);
        Assert.Contains("LEFT JOIN dbo.StockArea area", sql);
        Assert.Contains("CASE stock.Condition WHEN 1 THEN 'Bad' ELSE 'Good' END", sql);
        Assert.Contains("AS Condition", sql);
    }

    private static string ReadScript()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "GiselX.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        var path = Path.Combine(current!.FullName, "GiselX.Web", "DatabaseScripts", "Reporting_StockAndSellout.sql");
        Assert.True(File.Exists(path), $"Expected SQL script at '{path}'.");
        return File.ReadAllText(path);
    }
}
