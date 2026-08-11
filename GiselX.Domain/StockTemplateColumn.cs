namespace GiselX.Domain;

public sealed class StockTemplateColumn
{
    public int Id { get; set; }
    public int StockTemplateMappingId { get; set; }
    public int SourceColumnNumber { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public StockCanonicalField CanonicalField { get; set; }

    public StockTemplateMapping StockTemplateMapping { get; set; } = null!;
}
