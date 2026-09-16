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
