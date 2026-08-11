using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class SelloutUploadGroupDto
{
    public long GroupId { get; set; }
    public int CompanyId { get; set; }
    public SelloutUploadGroupStatus Status { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public IReadOnlyList<int> IgnoredSummaryRowNumbers { get; set; } = [];
    public IReadOnlyList<SelloutStagingErrorDto> Errors { get; set; } = [];
    public IReadOnlyList<SelloutUploadBatchDto> Batches { get; set; } = [];
}
