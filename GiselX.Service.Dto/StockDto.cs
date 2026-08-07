using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;

namespace GiselX.Service.Dto;

public class StockDto
{
    [Display(Name = "Product ID")]
    public string ProductId { get; set; } = string.Empty;
    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = string.Empty;
    [Display(Name = "Packaging")]
    public string ProductPackaging { get; set; } = string.Empty;
    [Display(Name = "Pcs/Ctn")]
    public int ProductPcsInCtn { get; set; }
    [Display(Name = "Netto")]
    public decimal ProductNetto { get; set; }
    [Display(Name = "Unit")]
    public string ProductUnit { get; set; } = string.Empty;
    [Display(Name = "Saldo Awal")]
    public decimal SaldoAwal { get; set; }
    [Display(Name = "Saldo Masuk PO")]
    public decimal SaldoMasukPO { get; set; }
    [Display(Name = "Saldo Akhir")]
    public decimal SaldoAkhir { get; set; }
    [Display(Name = "Batch No")]
    public string BatchNumber { get; set; } = string.Empty;
    [Display(Name = "Expired Date")]
    public DateTime ExpiredDate { get; set; } = SqlDateTime.MinValue.Value;
    public int CompanyId { get; set; }
    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;
}
