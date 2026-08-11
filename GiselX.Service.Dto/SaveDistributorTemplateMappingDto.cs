namespace GiselX.Service.Dto;

public sealed class SaveDistributorTemplateMappingDto
{
    public int? Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public string HeaderSignature { get; set; } = string.Empty;
    public List<DistributorTemplateColumnDto> Columns { get; set; } = [];
}
