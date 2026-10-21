namespace GiselX.Domain;

public sealed class StockStaging
{
    public long Id { get; set; }
    public long StockUploadBatchId { get; set; }
    public int? StockTemplateMappingId { get; set; }
    public string? SourceWorksheetName { get; set; }
    public int SourceRowNumber { get; set; }
    public string? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? DistributorProductId { get; set; }
    public string? DistributorProductName { get; set; }
    public int? ItemMappingId { get; set; }
    public string? ProductPackaging { get; set; }
    public decimal? ProductPcsInCtn { get; set; }
    public decimal? ProductNetto { get; set; }
    public string? ProductUnit { get; set; }
    public string? SmallestUnit { get; set; }
    public decimal? SmallestUnitQty { get; set; }
    public decimal? PriceUnit { get; set; }
    public decimal? SaldoAwal { get; set; }
    public decimal? SaldoMasukPO { get; set; }
    public decimal? SaldoAkhir { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public int? StockAreaId { get; set; }
    public StockCondition Condition { get; set; }
    public string? SourceAreaText { get; set; }
    public string? SourceConditionText { get; set; }
    public bool IsValid { get; set; }
    public string ValidationErrorsJson { get; set; } = "[]";

    public StockUploadBatch StockUploadBatch { get; set; } = null!;
}
