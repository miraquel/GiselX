namespace GiselX.Service.Dto;

public sealed class SelloutUploadPreflightDto
{
    public string? MappingName { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public IReadOnlyList<SelloutWeekSummaryDto> Weeks { get; set; } = [];
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<SelloutStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool IsMultiWeek => Weeks.Count > 1;
}
