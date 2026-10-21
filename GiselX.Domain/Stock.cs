using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlTypes;

namespace GiselX.Domain;

public class Stock
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? DistributorProductId { get; set; }
    public string? DistributorProductName { get; set; }
    public int? ItemMappingId { get; set; }
    public string ProductPackaging { get; set; } = string.Empty;
    public decimal ProductPcsInCtn { get; set; }
    public decimal ProductNetto { get; set; }
    public string ProductUnit { get; set; } = string.Empty;
    public string? SmallestUnit { get; set; }
    public decimal? SmallestUnitQty { get; set; }
    public decimal? PriceUnit { get; set; }
    public decimal SaldoAwal { get; set; }
    public decimal SaldoMasukPO { get; set; }
    public decimal SaldoAkhir { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiredDate { get; set; } = SqlDateTime.MinValue.Value;
    public int CompanyId { get; set; }
    public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;
    public long? StockUploadBatchId { get; set; }
    public DateTime? StockDate { get; set; }
    public int? StockAreaId { get; set; }
    public StockCondition Condition { get; set; }
    [NotMapped] public string CompanyName { get; set; } = string.Empty;
    [NotMapped] public string? AreaName { get; set; }

    public virtual Company Company { get; set; } = null!;
    public virtual ItemMapping? ItemMapping { get; set; }
    public virtual StockArea? StockArea { get; set; }
    public virtual StockUploadBatch? StockUploadBatch { get; set; }
}
