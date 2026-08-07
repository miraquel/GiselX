using System.Data.SqlTypes;

namespace GiselX.Domain;

public class Stock
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductPackaging { get; set; } = string.Empty;
    public int ProductPcsInCtn { get; set; }
    public decimal ProductNetto { get; set; }
    public string ProductUnit { get; set; } = string.Empty;
    public decimal SaldoAwal { get; set; }
    public decimal SaldoMasukPO { get; set; }
    public decimal SaldoAkhir { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiredDate { get; set; } = SqlDateTime.MinValue.Value;
    public int CompanyId { get; set; }
    public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;

    public virtual Company Company { get; set; } = null!;
}
