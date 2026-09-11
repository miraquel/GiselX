using System.ComponentModel.DataAnnotations.Schema;

namespace GiselX.Domain;

public sealed class StockUploadBatch
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int? StockTemplateMappingId { get; set; }
    public DateTime StockDate { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public StockUploadBatchStatus Status { get; set; } = StockUploadBatchStatus.Processing;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string IgnoredSummaryRowsJson { get; set; } = "[]";
    public string ValidationErrorsJson { get; set; } = "[]";
    public string? ErrorSummary { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    [NotMapped] public string CompanyName { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;
    public StockTemplateMapping? StockTemplateMapping { get; set; }
    public ICollection<StockStaging> StagingRows { get; set; } = new List<StockStaging>();
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
