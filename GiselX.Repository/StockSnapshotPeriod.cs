using Dapper;

namespace GiselX.Repository;

/// <summary>
/// Chooses which Stock snapshot each distributor is represented by. A snapshot is a position at one
/// moment, so several of them can never be added together: the grid, its count and the summary tiles
/// all show exactly one published batch per distributor and area, and all three must agree on which.
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
    /// The RankedBatches CTE; BatchRank 1 is the snapshot shown, per distributor and per area. A
    /// distributor can hold several live batches on one date, one per area, and one batch can carry
    /// several areas, so the unit ranked is the (batch, area) pair that still has Stock rows — a partly
    /// replaced batch ranks only the areas it still holds. With a month chosen the winner is the latest
    /// snapshot dated inside that month, otherwise the latest snapshot date overall (same order, no window).
    /// </summary>
    public static string RankedBatches(bool hasPeriod) => hasPeriod
        ? """
          WITH RankedBatches AS (
              SELECT b.Id, b.CompanyId, COALESCE(d.StockAreaId,0) AS AreaKey,
                     ROW_NUMBER() OVER (PARTITION BY b.CompanyId, COALESCE(d.StockAreaId,0) ORDER BY b.StockDate DESC, b.CompletedDate DESC, b.Id DESC) AS BatchRank
              FROM (SELECT DISTINCT StockUploadBatchId, StockAreaId FROM Stock WHERE StockUploadBatchId IS NOT NULL) d
              INNER JOIN StockUploadBatch b ON b.Id=d.StockUploadBatchId
              WHERE b.Status IN (2,3) AND b.StockDate >= @PeriodStart AND b.StockDate < @PeriodEnd
          )
          """
        : """
          WITH RankedBatches AS (
              SELECT b.Id, b.CompanyId, COALESCE(d.StockAreaId,0) AS AreaKey,
                     ROW_NUMBER() OVER (PARTITION BY b.CompanyId, COALESCE(d.StockAreaId,0) ORDER BY b.StockDate DESC, b.CompletedDate DESC, b.Id DESC) AS BatchRank
              FROM (SELECT DISTINCT StockUploadBatchId, StockAreaId FROM Stock WHERE StockUploadBatchId IS NOT NULL) d
              INNER JOIN StockUploadBatch b ON b.Id=d.StockUploadBatchId
              WHERE b.Status IN (2,3)
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
