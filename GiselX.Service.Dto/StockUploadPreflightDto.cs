namespace GiselX.Service.Dto;

public sealed class StockUploadPreflightDto
{
    public string? MappingName { get; set; }
    public DateTime StockDate { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool CanPublish => ValidRows > 0 && Errors.Count == 0;
}
