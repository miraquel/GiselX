using GiselX.Domain;

namespace GiselX.Web.Models;

public sealed class StockTemplateMappingEditViewModel
{
    public int CompanyId { get; set; }
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; } = 1;
    public int FirstDataRowNumber { get; set; } = 2;
    public string? GoodConditionValues { get; set; } = DefaultGoodConditionValues;
    public string? BadConditionValues { get; set; } = DefaultBadConditionValues;
    public List<StockTemplateColumnEditViewModel> Columns { get; set; } = [];

    /// <summary>Pre-fill for a new mapping in the editor only; resolution never falls back to these.</summary>
    public const string DefaultGoodConditionValues = "GOOD;GS;BAIK";
    public const string DefaultBadConditionValues = "BAD;BS;BAD STOCK;RUSAK";
}

public sealed class StockTemplateColumnEditViewModel
{
    public int SourceColumnNumber { get; set; }
    public string? SourceHeader { get; set; }
    public StockCanonicalField? CanonicalField { get; set; }
}
