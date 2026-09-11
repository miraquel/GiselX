namespace GiselX.Service.Dto;

public sealed class SalesSelectionSummaryDto
{
    public int DistributorCount { get; set; }
    public int RowCount { get; set; }
    public decimal InvoiceQty { get; set; }
    public decimal GrossValue { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal NetValue { get; set; }
}
