namespace GiselX.Service.Dto;

public sealed class StockUploadHistoryOptionDto
{
    public long BatchId { get; set; }
    public DateTime UploadedDate { get; set; }
    public DateTime StockDate { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string DisplayText => $"{UploadedDate:yyyy-MM-dd HH:mm} — {StockDate:yyyy-MM-dd} — {OriginalFileName}";
}
