using System.Data.SqlTypes;

namespace GiselX.Domain;

public class ServiceLevel
{
    public string SoId { get; set; } = string.Empty;
    public DateTime SoCreateDate { get; set; } = SqlDateTime.MinValue.Value;
    public int LeadTimeDlv { get; set; }
    public int LeadTimeRct { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal SoQty { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal KgPerUnit { get; set; }
    public DateTime DlvDateRequest { get; set; } = SqlDateTime.MinValue.Value;
    public DateTime RctDateRequest { get; set; } = SqlDateTime.MinValue.Value;
    public decimal DoQty { get; set; }
    public DateTime DoDate { get; set; } = SqlDateTime.MinValue.Value;
    public DateTime ReceiptDate { get; set; } = SqlDateTime.MinValue.Value;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = SqlDateTime.MinValue.Value;
}