using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class ItemMappingListViewModel
{
    public int CompanyId { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public IReadOnlyList<ItemMappingDto> Mappings { get; set; } = [];
}
