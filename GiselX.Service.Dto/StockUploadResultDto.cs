using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class StockUploadResultDto
{
    public long BatchId { get; set; }
    public StockUploadBatchStatus Status { get; set; }
    public DateTime StockDate { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<StockStagingErrorDto> Errors { get; set; } = [];
}
