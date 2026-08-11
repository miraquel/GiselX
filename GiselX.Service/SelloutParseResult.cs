using GiselX.Domain;
using GiselX.Service.Dto;

namespace GiselX.Service;

public sealed record SelloutParseResult(
    IReadOnlyList<SelloutStaging> Rows,
    IReadOnlyList<SelloutStagingErrorDto> Errors,
    string? BatchError,
    IReadOnlyList<SelloutWeekSummaryDto> WeekSummaries,
    IReadOnlyList<int> IgnoredSummaryRowNumbers);
