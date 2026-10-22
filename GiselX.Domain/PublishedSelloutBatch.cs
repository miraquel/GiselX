namespace GiselX.Domain;

/// <summary>A live Sellout batch together with how many of its published rows fall inside a date range.</summary>
public sealed class PublishedSelloutBatch
{
    public long BatchId { get; set; }
    public DateTime ReportingWeekStart { get; set; }
    public int RowCount { get; set; }
    public DateTime UploadedDate { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
}
