using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class DistributorTemplateColumnDto
{
    public int SourceColumnNumber { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public SelloutCanonicalField CanonicalField { get; set; }
}
