using GiselX.Domain;

namespace GiselX.Service.Dto;

public class SelloutUploadResultDto
{
    public long GroupId { get; set; }
    public long BatchId { get; set; }
    public SelloutUploadBatchStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<SelloutStagingErrorDto> Errors { get; set; } = [];
    public IReadOnlyList<SelloutWeekSummaryDto> Weeks { get; set; } = [];
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
}
