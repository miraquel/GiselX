using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class StockTemplateMappingListViewModel
{
    public int CompanyId { get; set; }
    public IReadOnlyList<StockTemplateMappingDto> Mappings { get; set; } = [];
}
