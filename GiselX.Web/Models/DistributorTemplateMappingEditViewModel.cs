using GiselX.Domain;

namespace GiselX.Web.Models;

public sealed class DistributorTemplateMappingEditViewModel
{
    public int CompanyId { get; set; }
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; } = 1;
    public int FirstDataRowNumber { get; set; } = 2;
    public List<DistributorTemplateColumnEditViewModel> Columns { get; set; } = [];
}

public sealed class DistributorTemplateColumnEditViewModel
{
    public int SourceColumnNumber { get; set; }
    public string? SourceHeader { get; set; }
    public SelloutCanonicalField? CanonicalField { get; set; }
}
