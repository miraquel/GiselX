namespace GiselX.Domain;

public sealed class SelloutUploadBatch
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int? DistributorTemplateMappingId { get; set; }
    public DateTime ReportingWeekStart { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public SelloutUploadBatchStatus Status { get; set; } = SelloutUploadBatchStatus.Processing;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string? ErrorSummary { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public Company Company { get; set; } = null!;
    public DistributorTemplateMapping? DistributorTemplateMapping { get; set; }
    public ICollection<SelloutStaging> StagingRows { get; set; } = new List<SelloutStaging>();
    public ICollection<SalesTransaction> SalesTransactions { get; set; } = new List<SalesTransaction>();
}
