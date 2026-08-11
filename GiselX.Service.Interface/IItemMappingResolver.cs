using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface IItemMappingResolver
{
    Task<ItemMappingContextDto> LoadAsync(int companyId, CancellationToken cancellationToken);
    ItemIdentityResolutionDto Resolve(ItemMappingContextDto context, string? distributorItemId,
        string? distributorItemName);
}
