using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// The supersede that stops Stock double-counting lives in hand-written SQL, and no test in this
/// suite executes SQL. These assertions pin its shape instead, because every property below was
/// necessary to get the behaviour right and losing any one of them brings the double-counting back
/// silently — a report that is merely wrong, not one that fails.
/// </summary>
public sealed class StockSupersedeSqlTests
{
    [Fact]
    public void Publication_deletes_the_previous_snapshot_and_marks_its_batch_replaced()
    {
        var sql = ReadRepository();

        Assert.Contains("DELETE stock FROM Stock stock", sql, StringComparison.Ordinal);
        Assert.Contains("UPDATE prior SET Status=4", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Publication_serialises_per_company_and_date_with_a_transaction_applock_before_deleting()
    {
        // Same-day uploads for different areas delete and insert overlapping key ranges; without a
        // lock they can deadlock (error 1205) and fail an upload the user did nothing wrong in.
        var sql = Regex.Replace(ReadRepository(), @"\s+", " ");

        Assert.Contains("N'GiselX-StockPublish-' + CAST(@CompanyId AS nvarchar(20)) + N'-' + CONVERT(nvarchar(8), @StockDate, 112)", sql, StringComparison.Ordinal);
        Assert.Contains("@LockMode = N'Exclusive'", sql, StringComparison.Ordinal);
        Assert.Contains("@LockOwner = N'Transaction'", sql, StringComparison.Ordinal);
        Assert.Contains("IF @LockResult < 0 THROW", sql, StringComparison.Ordinal);
        Assert.True(
            sql.IndexOf("sp_getapplock", StringComparison.Ordinal) > sql.IndexOf("SELECT @StockDate = StockDate", StringComparison.Ordinal)
            && sql.IndexOf("sp_getapplock", StringComparison.Ordinal) < sql.IndexOf("DELETE stock FROM Stock stock", StringComparison.Ordinal),
            "The lock must be taken after @StockDate is read and before any Stock row is deleted.");
    }

    [Fact]
    public void The_rows_go_before_the_batches_are_marked()
    {
        // Marking first would leave the delete matching status 4, which also catches batches
        // replaced by some earlier upload — whose rows are already gone — and would miss these.
        var sql = ReadRepository();

        Assert.True(
            sql.IndexOf("DELETE stock FROM Stock stock", StringComparison.Ordinal)
            < sql.IndexOf("UPDATE prior SET Status=4", StringComparison.Ordinal),
            "Stock rows must be deleted before their batches are marked Replaced.");
    }

    [Fact]
    public void Both_statements_are_scoped_to_one_company_and_one_snapshot_date()
    {
        // Without the scope this would wipe every other date, or every other distributor.
        var sql = ReadRepository();
        var scoped = Regex.Matches(sql,
            @"prior\.CompanyId=@CompanyId AND prior\.StockDate=@StockDate").Count;

        Assert.Equal(2, scoped);
    }

    [Fact]
    public void The_batch_being_published_is_excluded_and_only_live_batches_are_superseded()
    {
        // Without `prior.Id<>@BatchId` publication would delete the rows it had just inserted;
        // without the status filter it would resurrect batches that failed.
        var sql = ReadRepository();
        var guarded = Regex.Matches(sql, @"prior\.Id<>@BatchId AND prior\.Status IN \(2,3\)").Count;

        Assert.Equal(2, guarded);
    }

    [Fact]
    public void The_delete_is_limited_to_the_areas_of_this_uploads_valid_rows()
    {
        // Without this, a Binjai upload would wipe Medan's rows for the same date.
        var sql = ReadRepository();

        Assert.Contains("s.StockUploadBatchId=@BatchId AND s.IsValid=1", sql, StringComparison.Ordinal);
        Assert.Contains("COALESCE(s.StockAreaId,0)=COALESCE(stock.StockAreaId,0)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_prior_batch_is_replaced_only_when_none_of_its_rows_remain()
    {
        var sql = ReadRepository();

        Assert.Contains(
            "NOT EXISTS (SELECT 1 FROM Stock remaining WHERE remaining.StockUploadBatchId=prior.Id)",
            sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Publication_copies_area_and_condition_into_stock()
    {
        var sql = ReadRepository();
        var insert = sql.Substring(sql.IndexOf("INSERT INTO Stock (", StringComparison.Ordinal));
        var columns = insert.Substring(0, insert.IndexOf("SELECT", StringComparison.Ordinal));

        Assert.Contains("StockAreaId", columns, StringComparison.Ordinal);
        Assert.Contains("Condition", columns, StringComparison.Ordinal);
    }

    private static string ReadRepository() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "GiselX.Repository", "StockUploadRepository.cs"));

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
