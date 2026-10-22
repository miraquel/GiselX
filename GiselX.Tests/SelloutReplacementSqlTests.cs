using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// Sellout publication replaces the invoice-date range of the valid rows it carries. That lives in
/// hand-written SQL and no test in this suite executes SQL, so these assertions pin its shape: losing
/// any property below silently deletes, or keeps, another period's sales.
/// </summary>
public sealed class SelloutReplacementSqlTests
{
    [Fact]
    public void Publication_serialises_per_company_with_a_transaction_applock()
    {
        var sql = PromoteSql();

        Assert.Contains("sp_getapplock", sql, StringComparison.Ordinal);
        Assert.Contains("GiselX-SelloutPublish-", sql, StringComparison.Ordinal);
        Assert.Contains("@LockOwner = N'Transaction'", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 50003", sql, StringComparison.Ordinal);
        Assert.True(
            sql.IndexOf("sp_getapplock", StringComparison.Ordinal) < sql.IndexOf("DELETE", StringComparison.Ordinal),
            "The lock must be taken before anything is deleted.");
    }

    [Fact]
    public void The_range_comes_from_the_groups_valid_rows()
    {
        var sql = PromoteSql();
        var range = Slice(sql, "SELECT @From", ";");

        Assert.Contains("MIN(s.InvoiceDate)", range, StringComparison.Ordinal);
        Assert.Contains("MAX(s.InvoiceDate)", range, StringComparison.Ordinal);
        Assert.Contains("FROM SelloutStaging s JOIN SelloutUploadBatch b ON b.Id=s.SelloutUploadBatchId", range, StringComparison.Ordinal);
        Assert.Contains("b.SelloutUploadGroupId=@GroupId", range, StringComparison.Ordinal);
        Assert.Contains("s.IsValid=1", range, StringComparison.Ordinal);
    }

    [Fact]
    public void The_delete_is_limited_to_the_date_range()
    {
        var sql = PromoteSql();
        var delete = Slice(sql, "DELETE sales FROM SalesTransaction", ";");

        Assert.Contains("sales.CompanyId=@CompanyId", delete, StringComparison.Ordinal);
        Assert.Contains("sales.InvoiceDate >= @From", delete, StringComparison.Ordinal);
        Assert.Contains("sales.InvoiceDate < DATEADD(day,1,@To)", delete, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportingWeekStart=currentBatch.ReportingWeekStart", sql, StringComparison.Ordinal);
        Assert.Contains("@From IS NOT NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_touched_batches_that_lost_every_row_become_replaced()
    {
        var sql = PromoteSql();
        var touched = Slice(sql, "INSERT INTO @Touched", ";");
        var mark = Slice(sql, "UPDATE prior SET Status=3", ";");

        Assert.Contains("DECLARE @Touched TABLE", sql, StringComparison.Ordinal);
        Assert.Contains("prior.Status IN (2,4)", touched, StringComparison.Ordinal);
        Assert.Contains("prior.SelloutUploadGroupId<>@GroupId", touched, StringComparison.Ordinal);
        Assert.Contains("prior.CompanyId=@CompanyId", touched, StringComparison.Ordinal);
        Assert.Contains("sales.InvoiceDate >= @From", touched, StringComparison.Ordinal);
        Assert.Contains("JOIN @Touched", mark, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS (SELECT 1 FROM SalesTransaction", mark, StringComparison.Ordinal);
        Assert.Contains("SelloutUploadBatchId=prior.Id", mark, StringComparison.Ordinal);
        Assert.True(
            sql.IndexOf("INSERT INTO @Touched", StringComparison.Ordinal) < sql.IndexOf("DELETE sales FROM SalesTransaction", StringComparison.Ordinal)
            && sql.IndexOf("DELETE sales FROM SalesTransaction", StringComparison.Ordinal) < sql.IndexOf("UPDATE prior SET Status=3", StringComparison.Ordinal),
            "Batches are recorded, then the rows deleted, then the emptied batches marked.");
    }

    [Fact]
    public void Staging_rows_are_deleted_only_for_batches_that_became_replaced()
    {
        var sql = PromoteSql();
        var delete = Slice(sql, "DELETE staging FROM SelloutStaging", ";");

        Assert.Contains("JOIN @Touched", delete, StringComparison.Ordinal);
        Assert.Contains("replaced.Status=3", delete, StringComparison.Ordinal);
        Assert.True(
            sql.IndexOf("UPDATE prior SET Status=3", StringComparison.Ordinal) < sql.IndexOf("DELETE staging FROM SelloutStaging", StringComparison.Ordinal),
            "Staging goes after the batches are marked.");
        Assert.True(
            sql.IndexOf("DELETE staging FROM SelloutStaging", StringComparison.Ordinal) < sql.IndexOf("INSERT INTO SalesTransaction", StringComparison.Ordinal),
            "The new rows are inserted last.");
    }

    private static string Slice(string sql, string start, string end)
    {
        var from = sql.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' was not found.");
        var to = sql.IndexOf(end, from, StringComparison.Ordinal);
        return sql.Substring(from, to - from);
    }

    private static string PromoteSql()
    {
        var file = File.ReadAllText(Path.Combine(RepositoryRoot(), "GiselX.Repository", "SelloutUploadRepository.cs"));
        var start = file.IndexOf("PromoteGroupAsync(long groupId", StringComparison.Ordinal);
        var end = file.IndexOf("GetBatchAsync", start, StringComparison.Ordinal);
        return Regex.Replace(file.Substring(start, end - start), @"\s+", " ");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("GiselX.sln was not found.");
    }
}
