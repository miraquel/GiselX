namespace GiselX.Service.Dto;

public sealed class StockUploadPreflightDto
{
    /// <summary>
    /// SHA-256 of the workbook exactly as it was sent. The upload must quote it back, which is what
    /// ties the file a user reviewed to the file that is actually published.
    /// </summary>
    public string FileSha256 { get; set; } = string.Empty;

    public string? MappingName { get; set; }
    public DateTime StockDate { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public decimal TotalSaldoAwal { get; set; }
    public decimal TotalSaldoMasukPo { get; set; }
    public decimal TotalSaldoAkhir { get; set; }

    /// <summary>
    /// One entry per mapped worksheet the workbook contributes to this snapshot.
    /// </summary>
    public IReadOnlyList<StockSheetAnalysisDto> Sheets { get; set; } = [];

    /// <summary>
    /// Worksheets skipped because no active mapping matched them. Shown as information, not as an error.
    /// </summary>
    public IReadOnlyList<string> IgnoredWorksheetNames { get; set; } = [];

    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool CanPublish => ValidRows > 0 && Errors.Count == 0;
}
