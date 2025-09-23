namespace GiselX.Domain;

public partial class ServelDeliveryEx
{
    public int Id { get; set; }
    public string? SoId { get; set; }

    public string? ItemId { get; set; }

    public string? ItemName { get; set; }

    public decimal? SoQty { get; set; }

    public decimal? SoQtyCalc { get; set; }

    public DateTime? SoCreateDate { get; set; }

    public DateTime? ShippingDateRequest { get; set; }

    public int? LeadTimeDelivery { get; set; }

    public DateTime? ShippingDateRequestCalc { get; set; }

    public DateTime? ReceiptDateRequest { get; set; }

    public int? LeadTimeReceipt { get; set; }

    public DateTime? ReceiptDateRequestCalc { get; set; }

    public decimal? DoQty { get; set; }

    public decimal? DoQtyCalc { get; set; }

    public DateTime? DoDate { get; set; }

    public DateTime? DoDateMin { get; set; }

    public DateTime? ReceiptDate { get; set; }

    public DateOnly? PeriodDate { get; set; }

    public int? Ontime { get; set; }

    public int? Infull { get; set; }

    public string? QuadranServel { get; set; }
}
