namespace GiselX.Domain;

public sealed class SelloutStaging
{
    public long Id { get; set; }
    public long SelloutUploadBatchId { get; set; }
    public int SourceRowNumber { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerAlias { get; set; }
    public string? CustomerAddress { get; set; }
    public string? ProductId { get; set; }
    public string? ProductBrand { get; set; }
    public string? ProductName { get; set; }
    public string? DistributorProductId { get; set; }
    public string? DistributorProductName { get; set; }
    public int? ItemMappingId { get; set; }
    public string? ProductPackaging { get; set; }
    public decimal? ProductPcsInCtn { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal? InvoiceQty { get; set; }
    public string? InvoiceUnit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? GrossValue { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? DiscountPct { get; set; }
    public decimal? NetValue { get; set; }
    public decimal? InKg { get; set; }
    public string? GeisaPOId { get; set; }
    public DateTime? ShipDate { get; set; }
    public string? SalesmanNameGMK { get; set; }
    public bool IsValid { get; set; }
    public string ValidationErrorsJson { get; set; } = "[]";

    public SelloutUploadBatch SelloutUploadBatch { get; set; } = null!;
}
