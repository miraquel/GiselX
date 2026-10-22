using GiselX.Domain;

namespace GiselX.Service.Dto;

/// <summary>What confirming a Sellout upload will replace: the invoice-date range of its valid rows and what is published there.</summary>
public sealed class SelloutReplacementDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<PublishedSelloutBatch> Replaces { get; set; } = [];
}
