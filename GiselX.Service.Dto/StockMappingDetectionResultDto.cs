namespace GiselX.Service.Dto;

public sealed class StockMappingDetectionResultDto
{
    /// <summary>
    /// Every active mapping whose configured worksheet matched, one entry per worksheet. A workbook may
    /// carry several mapped worksheets and all of them are published as one snapshot.
    /// </summary>
    public List<StockSheetMatchDto> Matches { get; set; } = [];

    /// <summary>
    /// Worksheets present in the workbook that no active mapping matched. These are skipped rather than
    /// rejected, because distributor files routinely carry cover and summary tabs.
    /// </summary>
    public List<string> IgnoredWorksheetNames { get; set; } = [];

    public List<string> CandidateMappingNames { get; set; } = [];
    public List<string> Errors { get; set; } = [];

    public bool IsMatch => Matches.Count > 0 && Errors.Count == 0;

    /// <summary>
    /// The single matched mapping, or null when the workbook matched several. Callers that record one
    /// mapping per batch use this and store null for a multi-worksheet upload.
    /// </summary>
    public StockTemplateMappingDto? Mapping => Matches.Count == 1 ? Matches[0].Mapping : null;
}

public sealed class StockSheetMatchDto
{
    public StockTemplateMappingDto Mapping { get; set; } = null!;

    /// <summary>
    /// The worksheet name as it actually appears in the workbook. Worksheet resolution tolerates
    /// letter-case and surrounding-whitespace differences, so this can differ from the mapping's.
    /// </summary>
    public string WorksheetName { get; set; } = string.Empty;
}
