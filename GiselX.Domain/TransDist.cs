namespace GiselX.Domain;

public partial class TransDist
{
    public int Id { get; set; }

    public string? SoId { get; set; }

    public DateTime? SoCreateDate { get; set; }

    public int? LeadTimeDlv { get; set; }

    public int? LeadTimeRct { get; set; }

    public string? ItemId { get; set; }

    public string? ItemName { get; set; }

    public decimal? SoQty { get; set; }

    public string? Unit { get; set; }

    public decimal? KgPerUnit { get; set; }

    public DateTime? DlvDateRequest { get; set; }

    public DateTime? RctDateRequest { get; set; }

    public decimal? DoQty { get; set; }

    public DateTime? DoDate { get; set; }

    public DateTime? ReceiptDate { get; set; }

    public int CompanyId { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual Company Company { get; set; } = null!;
}
