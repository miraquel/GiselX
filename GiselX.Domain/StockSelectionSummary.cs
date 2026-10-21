namespace GiselX.Domain;

public sealed class StockSelectionSummary
{
    public int DistributorCount { get; set; }
    public int RowCount { get; set; }
    public decimal SaldoAwal { get; set; }
    public decimal SaldoMasukPO { get; set; }
    public decimal SaldoAkhir { get; set; }
    public decimal SaldoAkhirGood { get; set; }
    public decimal SaldoAkhirBad { get; set; }
    public decimal StockValue { get; set; }
}
