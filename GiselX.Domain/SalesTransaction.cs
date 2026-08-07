using System.Data.SqlTypes;

namespace GiselX.Domain;

public class SalesTransaction
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerAlias { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductBrand { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductPackaging { get; set; } = string.Empty;
    public int ProductPcsInCtn { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiredDate { get; set; } = SqlDateTime.MinValue.Value;
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = SqlDateTime.MinValue.Value;
    public decimal InvoiceQty { get; set; }
    public string InvoiceUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal GrossValue { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal NetValue { get; set; }
    public decimal InKg { get; set; }
    public string GeisaPOId { get; set; } = string.Empty;
    public DateTime ShipDate { get; set; } = SqlDateTime.MinValue.Value;
    public string SalesmanNameGMK { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;

    public virtual Company Company { get; set; } = null!;
}
