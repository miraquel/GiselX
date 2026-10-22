using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// Nothing in the suite executes SQL, so the preview's "what this replaces" query is pinned by its text:
/// the range must agree with the one PromoteGroupAsync deletes, and it must see only live batches.
/// </summary>
public sealed class SelloutPublishedInRangeSqlTests
{
    [Fact]
    public void GetPublishedInRangeAsync_selects_the_whole_of_both_end_dates_of_live_batches_of_one_company()
    {
        var sql = Normalize(QueryText());

        Assert.Contains("InvoiceDate >= @From", sql, StringComparison.Ordinal);
        Assert.Contains("InvoiceDate < DATEADD(day, 1, @To)", sql, StringComparison.Ordinal);
        Assert.Contains("b.Status IN (2, 4)", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE CompanyId = @CompanyId", sql, StringComparison.Ordinal);
        Assert.Contains("b.CompanyId = @CompanyId", sql, StringComparison.Ordinal);
    }

    private static string QueryText()
    {
        var source = File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", "SelloutUploadRepository.cs"));
        var start = source.IndexOf("GetPublishedInRangeAsync(", StringComparison.Ordinal);
        Assert.True(start >= 0, "GetPublishedInRangeAsync was not found.");
        var open = source.IndexOf("\"\"\"", start, StringComparison.Ordinal);
        var close = source.IndexOf("\"\"\"", open + 3, StringComparison.Ordinal);
        return source[(open + 3)..close];
    }

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
