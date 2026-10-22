namespace GiselX.Service.Dto;

public sealed class SelloutUploadPreflightDto
{
    /// <summary>
    /// SHA-256 of the workbook exactly as it was sent. The upload must quote it back, which is what
    /// ties the file a user reviewed to the file that is actually published.
    /// </summary>
    public string FileSha256 { get; set; } = string.Empty;

    public string? MappingName { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public decimal TotalInvoiceQty { get; set; }
    public decimal TotalGrossValue { get; set; }
    public decimal TotalDiscountValue { get; set; }
    public decimal TotalNetValue { get; set; }
    public IReadOnlyList<SelloutWeekSummaryDto> Weeks { get; set; } = [];
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<SelloutStagingErrorDto> RowErrors { get; set; } = [];

    /// <summary>
    /// <see cref="RowErrors"/> as readable lines. Below the invalid-row limit these rows are skipped, not
    /// fatal, so the preview lists them beside a Confirm that still publishes the rest.
    /// </summary>
    public IReadOnlyList<string> RowErrorMessages { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool IsMultiWeek => Weeks.Count > 1;

    /// <summary>The date range confirming will replace and what is published there; null when the analysis has errors.</summary>
    public SelloutReplacementDto? Replacement { get; set; }
}
