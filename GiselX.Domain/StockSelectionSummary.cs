namespace GiselX.Domain;

public sealed class StockSelectionSummary
{
    public int DistributorCount { get; set; }
    public int RowCount { get; set; }
    public decimal QtyCarton { get; set; }
    public decimal QtyPcs { get; set; }
    public decimal QtyCartonBad { get; set; }
    public decimal QtyPcsBad { get; set; }
}
