namespace GiselX.Service.Dto;

public sealed class UnmappedDistributorItemListDto
{
    public bool ItemMappingEnabled { get; set; }
    public IReadOnlyList<UnmappedDistributorItemDto> Items { get; set; } = [];
}
