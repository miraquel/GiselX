namespace GiselX.Domain;

public sealed class DistributorTemplateColumn
{
    public int Id { get; set; }
    public int DistributorTemplateMappingId { get; set; }
    public int SourceColumnNumber { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public SelloutCanonicalField CanonicalField { get; set; }

    public DistributorTemplateMapping DistributorTemplateMapping { get; set; } = null!;
}
