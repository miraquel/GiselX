using Dapper;

namespace GiselX.Repository;

/// <summary>
/// Chooses which Stock snapshot each distributor is represented by. A snapshot is a position at one
/// moment, so several of them can never be added together: the grid, its count and the summary tiles
/// all show exactly one published batch per distributor, and all three must agree on which.
/// </summary>
public static class StockSnapshotPeriod
{
    /// <summary>
    /// Reads the Year and Month filters the Stock page sends. Both are required — a year alone is no
    /// month to pick a snapshot from — and the window is half-open so a date column compares cleanly.
    /// </summary>
    public static bool TryRead(IReadOnlyDictionary<string, string> filters, out DateTime start, out DateTime end)
    {
        start = end = default;
        if (!filters.TryGetValue("Year", out var yearText) || !int.TryParse(yearText, out var year) ||
            !filters.TryGetValue("Month", out var monthText) || !int.TryParse(monthText, out var month) ||
            year is < 1900 or > 9998 || month is < 1 or > 12)
            return false;

        start = new DateTime(year, month, 1);
        end = start.AddMonths(1);
        return true;
    }

    /// <summary>
    /// The RankedBatches CTE; BatchRank 1 is the snapshot shown. With a month chosen it is the latest
    /// snapshot dated inside that month — the month-end position — otherwise the most recently
    /// published batch, which is what the page showed before it could be browsed by month.
    /// </summary>
    public static string RankedBatches(bool hasPeriod) => hasPeriod
        ? """
          WITH RankedBatches AS (
              SELECT Id, CompanyId,
                     ROW_NUMBER() OVER (PARTITION BY CompanyId ORDER BY StockDate DESC, CompletedDate DESC, Id DESC) AS BatchRank
              FROM StockUploadBatch
              WHERE Status IN (2,3) AND StockDate >= @PeriodStart AND StockDate < @PeriodEnd
          )
          """
        : """
          WITH RankedBatches AS (
              SELECT Id, CompanyId,
                     ROW_NUMBER() OVER (PARTITION BY CompanyId ORDER BY CompletedDate DESC, Id DESC) AS BatchRank
              FROM StockUploadBatch
              WHERE Status IN (2,3)
          )
          """;

    /// <summary>Adds the period parameters when a month was chosen and returns the matching CTE.</summary>
    internal static string RankedBatchesFor(IReadOnlyDictionary<string, string> filters, DynamicParameters parameters)
    {
        if (!TryRead(filters, out var start, out var end)) return RankedBatches(hasPeriod: false);
        parameters.Add("PeriodStart", start);
        parameters.Add("PeriodEnd", end);
        return RankedBatches(hasPeriod: true);
    }
}
