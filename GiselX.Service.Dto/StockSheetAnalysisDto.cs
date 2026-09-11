namespace GiselX.Service.Dto;

/// <summary>
/// Per-worksheet breakdown of one Stock workbook analysis, so the preflight review can show what each
/// mapped worksheet contributed before the user confirms a single combined snapshot.
/// </summary>
public sealed class StockSheetAnalysisDto
{
    public string WorksheetName { get; set; } = string.Empty;
    public int MappingId { get; set; }
    public string MappingName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
}
