using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class SelloutUploadBatchDto : SelloutUploadResultDto
{
    public int CompanyId { get; set; }
    public string? MappingName { get; set; }
    public DateTime ReportingWeekStart { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}
