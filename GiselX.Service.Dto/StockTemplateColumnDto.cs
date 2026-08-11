using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class StockTemplateColumnDto
{
    public int SourceColumnNumber { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public StockCanonicalField CanonicalField { get; set; }
}
