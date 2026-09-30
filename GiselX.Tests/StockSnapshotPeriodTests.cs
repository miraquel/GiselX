using System.Text.RegularExpressions;
using GiselX.Repository;

namespace GiselX.Tests;

/// <summary>
/// The Stock page is browsed by month, like Sellout. A month can hold several snapshots per
/// distributor (the 7th, the 14th, the 30th); only the latest one in that month is a stock position,
/// and summing them would count the same goods more than once.
/// </summary>
public sealed class StockSnapshotPeriodTests
{
    [Fact]
    public void Year_and_month_become_a_half_open_month_window()
    {
        var found = StockSnapshotPeriod.TryRead(Filters(("Year", "2026"), ("Month", "9")), out var start, out var end);

        Assert.True(found);
        Assert.Equal(new DateTime(2026, 9, 1), start);
        Assert.Equal(new DateTime(2026, 10, 1), end);
    }

    [Fact]
    public void December_rolls_into_the_next_year()
    {
        StockSnapshotPeriod.TryRead(Filters(("Year", "2026"), ("Month", "12")), out _, out var end);

        Assert.Equal(new DateTime(2027, 1, 1), end);
    }

    [Theory]
    [InlineData("2026", null)]
    [InlineData(null, "9")]
    [InlineData("2026", "13")]
    [InlineData("2026", "0")]
    [InlineData("abc", "9")]
    public void Anything_short_of_a_valid_year_and_month_is_no_period(string? year, string? month)
    {
        var filters = new Dictionary<string, string>();
        if (year is not null) filters["Year"] = year;
        if (month is not null) filters["Month"] = month;

        Assert.False(StockSnapshotPeriod.TryRead(filters, out _, out _));
    }

    [Fact]
    public void A_chosen_month_ranks_each_distributors_snapshots_inside_it_latest_first()
    {
        var sql = Normalize(StockSnapshotPeriod.RankedBatches(hasPeriod: true));

        Assert.Contains("StockDate >= @PeriodStart AND StockDate < @PeriodEnd", sql, StringComparison.Ordinal);
        Assert.Contains("PARTITION BY CompanyId ORDER BY StockDate DESC, CompletedDate DESC, Id DESC", sql, StringComparison.Ordinal);
        Assert.Contains("Status IN (2,3)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_month_the_latest_published_batch_per_distributor_is_kept_as_before()
    {
        var sql = Normalize(StockSnapshotPeriod.RankedBatches(hasPeriod: false));

        Assert.Contains("PARTITION BY CompanyId ORDER BY CompletedDate DESC, Id DESC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@PeriodStart", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Grid_count_and_summary_all_rank_through_the_shared_period_aware_batches()
    {
        // Three statements read the snapshot set: the page, its count and the summary tiles. One of
        // them keeping its own copy would show totals for a different month than the rows.
        var source = File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", "StockRepository.cs"));

        Assert.DoesNotContain("WITH RankedBatches AS", source, StringComparison.Ordinal);
        Assert.Contains("StockSnapshotPeriod.RankedBatchesFor(", MethodBody(source, "SelectAsync"), StringComparison.Ordinal);
        Assert.Contains("StockSnapshotPeriod.RankedBatchesFor(", MethodBody(source, "SummarizeAsync"), StringComparison.Ordinal);
    }

    [Fact]
    public void Periods_are_the_snapshot_dates_that_were_published()
    {
        var body = MethodBody(File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", "StockRepository.cs")),
            "SelectPeriodsAsync");

        Assert.Contains("YEAR(StockDate)", body, StringComparison.Ordinal);
        Assert.Contains("FROM StockUploadBatch", body, StringComparison.Ordinal);
        Assert.Contains("Status IN (2,3)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatedDate", body, StringComparison.Ordinal);
        Assert.DoesNotContain("GiselStock_SelectPeriods", body, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Filters(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value);

    private static string MethodBody(string source, string method)
    {
        var start = source.IndexOf($" {method}(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{method} not found.");
        return source[start..source.IndexOf("\n    }", start, StringComparison.Ordinal)];
    }

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ");

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
