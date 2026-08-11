using GiselX.Service.Dto;

namespace GiselX.Service;

public sealed record StockParseResult(
    IReadOnlyList<StockStagingRowDto> Rows,
    IReadOnlyList<StockStagingErrorDto> Errors,
    string? BatchError,
    IReadOnlyList<int> IgnoredSummaryRowNumbers)
{
    public int ValidRows => Rows.Count(row => row.IsValid);
    public int InvalidRows => Rows.Count - ValidRows;
}
