namespace GiselX.Service.Dto;

public sealed class StockWorkbookAnalysisDto
{
    /// <summary>
    /// The single mapping that produced this analysis, or null when rows came from several mapped
    /// worksheets. <see cref="Sheets"/> is the complete picture in either case.
    /// </summary>
    public StockTemplateMappingDto? Mapping { get; set; }

    public IReadOnlyList<StockSheetAnalysisDto> Sheets { get; set; } = [];
    public IReadOnlyList<string> IgnoredWorksheetNames { get; set; } = [];
    public IReadOnlyList<StockStagingRowDto> Rows { get; set; } = [];
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public int ValidRows => Rows.Count(row => row.IsValid);
    public int InvalidRows => Rows.Count - ValidRows;
    public bool CanPublish => Errors.Count == 0 && ValidRows > 0;
}
