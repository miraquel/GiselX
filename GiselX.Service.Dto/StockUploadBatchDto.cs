using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class StockUploadBatchDto
{
    public long BatchId { get; set; }
    public int CompanyId { get; set; }
    public string? MappingName { get; set; }
    public DateTime StockDate { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public StockUploadBatchStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> Errors { get; set; } = [];
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}
