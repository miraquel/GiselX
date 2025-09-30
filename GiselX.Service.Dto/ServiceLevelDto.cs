using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;

namespace GiselX.Service.Dto;

public class ServiceLevelDto
{
    [Display(Name = "SO ID")]
    public string SoId { get; set; } = string.Empty;
    [Display(Name = " Created Date")]
    public DateTime SoCreateDate { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "Lead Time Delivery")]
    public int LeadTimeDlv { get; set; }
    [Display(Name = "Lead Time Receipt")]
    public int LeadTimeRct { get; set; }
    [Display(Name = "Item ID")]
    public string ItemId { get; set; } = string.Empty;
    [Display(Name = "Item Name")]
    public string ItemName { get; set; } = string.Empty;
    [Display(Name = "SO Quantity")]
    public decimal SoQty { get; set; }
    [Display(Name = "Unit")]
    public string Unit { get; set; } = string.Empty;
    [Display(Name = "Kg/Unit")]
    public decimal KgPerUnit { get; set; }
    [Display(Name = "Delivery Date Request")]
    public DateTime DlvDateRequest { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "Receipt Date Request")]
    public DateTime RctDateRequest { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "DO Quantity")]
    public decimal DoQty { get; set; }
    [Display(Name = "DO Date")]
    public DateTime DoDate { get; set; } = SqlDateTime.MinValue.Value;
    // ReceiptDate
    [Display(Name = "Receipt Date")]
    public DateTime ReceiptDate { get; set; } = SqlDateTime.MinValue.Value;
    [Display(Name = "Created By")]
    public string CreatedBy { get; set; } = string.Empty;
    [Display(Name = "Created Date")] public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;
    public int CompanyId { get; set; }
}