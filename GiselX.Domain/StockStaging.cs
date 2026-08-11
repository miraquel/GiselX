namespace GiselX.Domain;

public sealed class StockStaging
{
    public long Id { get; set; }
    public long StockUploadBatchId { get; set; }
    public int SourceRowNumber { get; set; }
    public string? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductPackaging { get; set; }
    public int? ProductPcsInCtn { get; set; }
    public decimal? ProductNetto { get; set; }
    public string? ProductUnit { get; set; }
    public decimal? PriceUnit { get; set; }
    public decimal? SaldoAwal { get; set; }
    public decimal? SaldoMasukPO { get; set; }
    public decimal? SaldoAkhir { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public bool IsValid { get; set; }
    public string ValidationErrorsJson { get; set; } = "[]";

    public StockUploadBatch StockUploadBatch { get; set; } = null!;
}
