namespace GiselX.Domain;

/// <summary>
/// One area's rows from one live Stock batch for a snapshot date: what an upload of that area
/// would replace.
/// </summary>
public sealed class PublishedStockArea
{
    public int? StockAreaId { get; set; }
    public string? AreaName { get; set; }
    public int RowCount { get; set; }
    public DateTime UploadedDate { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
}
