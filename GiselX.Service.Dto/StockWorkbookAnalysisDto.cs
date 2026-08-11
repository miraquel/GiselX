namespace GiselX.Service.Dto;

public sealed class StockWorkbookAnalysisDto
{
    public StockTemplateMappingDto? Mapping { get; set; }
    public IReadOnlyList<StockStagingRowDto> Rows { get; set; } = [];
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public int ValidRows => Rows.Count(row => row.IsValid);
    public int InvalidRows => Rows.Count - ValidRows;
    public bool CanPublish => Mapping is not null && Errors.Count == 0 && ValidRows > 0;
}
