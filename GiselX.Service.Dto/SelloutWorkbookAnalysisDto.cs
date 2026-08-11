using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class SelloutWorkbookAnalysisDto
{
    public DistributorTemplateMappingDto? Mapping { get; set; }
    public IReadOnlyList<SelloutStaging> Rows { get; set; } = [];
    public IReadOnlyList<SelloutWeekSummaryDto> WeekSummaries { get; set; } = [];
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<SelloutStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool IsMultiWeek => WeekSummaries.Count > 1;
    public bool IsValid => Mapping is not null && Errors.Count == 0 && RowErrors.Count == 0 && Rows.Count > 0;
}
