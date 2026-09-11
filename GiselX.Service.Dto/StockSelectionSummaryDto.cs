namespace GiselX.Service.Dto;

public sealed class StockSelectionSummaryDto
{
    public int DistributorCount { get; set; }
    public int RowCount { get; set; }
    public decimal SaldoAwal { get; set; }
    public decimal SaldoMasukPO { get; set; }
    public decimal SaldoAkhir { get; set; }
    public decimal StockValue { get; set; }
}
