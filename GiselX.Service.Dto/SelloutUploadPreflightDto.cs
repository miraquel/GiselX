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
    public IReadOnlyList<string> Errors { get; set; } = [];
    public bool IsMultiWeek => Weeks.Count > 1;

    /// <summary>
    /// Non-blocking cautions shown alongside the summary. Unlike <see cref="Errors"/> these do not hide
    /// the confirm button; they tell the user what confirming will destroy.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];
}
