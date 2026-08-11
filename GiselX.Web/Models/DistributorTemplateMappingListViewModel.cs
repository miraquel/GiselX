using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class DistributorTemplateMappingListViewModel
{
    public int CompanyId { get; set; }
    public IReadOnlyList<DistributorTemplateMappingDto> Mappings { get; set; } = [];
}
