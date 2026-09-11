namespace GiselX.Service.Dto;

public sealed class SelloutUploadPreflightDto
{
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
