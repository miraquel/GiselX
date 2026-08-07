using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;

namespace GiselX.Service.Dto;

public class SalesTransactionDto
{
    [Display(Name = "Customer ID")]
    public string CustomerId { get; set; } = string.Empty;
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;
    [Display(Name = "Customer Alias")]
    public string CustomerAlias { get; set; } = string.Empty;
    [Display(Name = "Customer Address")]
    public string CustomerAddress { get; set; } = string.Empty;
    [Display(Name = "Product ID")]
    public string ProductId { get; set; } = string.Empty;
    [Display(Name = "Product Brand")]
    public string ProductBrand { get; set; } = string.Empty;
    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = string.Empty;
    [Display(Name = "Product Packaging")]
    public string ProductPackaging { get; set; } = string.Empty;
    [Display(Name = "Pcs/Ctn")]
    public int ProductPcsInCtn { get; set; }
    [Display(Name = "Batch No")]
    public string BatchNumber { get; set; } = string.Empty;
    [Display(Name = "Expired Date")]
    public DateTime ExpiredDate { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "Invoice No")]
    public string InvoiceNo { get; set; } = string.Empty;
    [Display(Name = "Invoice Date")]
    public DateTime InvoiceDate { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "Invoice Qty")]
    public decimal InvoiceQty { get; set; }
    [Display(Name = "Unit")]
    public string InvoiceUnit { get; set; } = string.Empty;
    [Display(Name = "Unit Price")]
    public decimal UnitPrice { get; set; }
    [Display(Name = "Gross Value")]
    public decimal GrossValue { get; set; }
    [Display(Name = "Discount Value")]
    public decimal DiscountValue { get; set; }
    [Display(Name = "Discount %")]
    public decimal DiscountPct { get; set; }
    [Display(Name = "Net Value")]
    public decimal NetValue { get; set; }
    [Display(Name = "In Kg")]
    public decimal InKg { get; set; }
    [Display(Name = "Geisa PO ID")]
    public string GeisaPOId { get; set; } = string.Empty;
    [Display(Name = "Ship Date")]
    public DateTime ShipDate { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "Salesman")]
    public string SalesmanNameGMK { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;
}
