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
    /// <summary>Cartons across the valid rows; zero when the template maps no carton column.</summary>
    public decimal TotalQtyCarton { get; set; }
    /// <summary>Pieces across the valid rows; zero when the template maps no piece column.</summary>
    public decimal TotalQtyPcs { get; set; }

    /// <summary>
    /// One entry per mapped worksheet the workbook contributes to this snapshot.
    /// </summary>
    public IReadOnlyList<StockSheetAnalysisDto> Sheets { get; set; } = [];

    /// <summary>
    /// Worksheets skipped because no active mapping matched them. Shown as information, not as an error.
    /// </summary>
    public IReadOnlyList<string> IgnoredWorksheetNames { get; set; } = [];

    /// <summary>One entry per area the valid rows carry, with the published rows it would replace.</summary>
    public IReadOnlyList<StockAreaReplacementDto> Areas { get; set; } = [];

    /// <summary>Areas published for this date that this upload does not contain, so leaves alone.</summary>
    public IReadOnlyList<string> UntouchedAreaNames { get; set; } = [];

    /// <summary>Active areas with nothing for this date, neither in this upload nor published earlier.</summary>
    public IReadOnlyList<string> MissingAreaNames { get; set; } = [];

    /// <summary>Mapped worksheets whose template has no Condition column, so every row counts as Good.</summary>
    public IReadOnlyList<string> WorksheetsWithoutCondition { get; set; } = [];

    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> RowErrors { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool CanPublish => ValidRows > 0 && Errors.Count == 0;
}
