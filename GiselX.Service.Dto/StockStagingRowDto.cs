namespace GiselX.Service.Dto;

public sealed class StockStagingRowDto
{
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
    public bool IsValid { get; set; }
    public string ValidationErrorsJson { get; set; } = "[]";
}
