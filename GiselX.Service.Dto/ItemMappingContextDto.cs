namespace GiselX.Service.Dto;

public sealed class ItemMappingContextDto
{
    public bool IsEnabled { get; init; }
    public IReadOnlyList<ItemMappingDto> Mappings { get; init; } = [];
}
