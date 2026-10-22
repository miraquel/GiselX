using GiselX.Domain;

namespace GiselX.Service.Dto;

/// <summary>One area an upload carries, with what publishing it would replace.</summary>
public sealed class StockAreaReplacementDto
{
    /// <summary>"(no area)" when the rows carry no area.</summary>
    public string AreaName { get; set; } = string.Empty;
    public int ValidRows { get; set; }
    public decimal QtyCartonGood { get; set; }
    public decimal QtyPcsGood { get; set; }
    public decimal QtyCartonBad { get; set; }
    public decimal QtyPcsBad { get; set; }
    public IReadOnlyList<PublishedStockArea> Replaces { get; set; } = [];
}
