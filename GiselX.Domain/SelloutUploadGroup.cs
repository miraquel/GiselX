namespace GiselX.Domain;

public sealed class SelloutUploadGroup
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int? DistributorTemplateMappingId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public SelloutUploadGroupStatus Status { get; set; } = SelloutUploadGroupStatus.Processing;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string IgnoredSummaryRowsJson { get; set; } = "[]";
    public string ValidationErrorsJson { get; set; } = "[]";
    public string? ErrorSummary { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public Company Company { get; set; } = null!;
    public DistributorTemplateMapping? DistributorTemplateMapping { get; set; }
    public ICollection<SelloutUploadBatch> Batches { get; set; } = new List<SelloutUploadBatch>();
}
