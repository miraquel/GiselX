using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class ItemMappingResolver(
    ICompanyRepository companyRepository,
    IItemMappingRepository itemMappingRepository) : IItemMappingResolver
{
    public async Task<ItemMappingContextDto> LoadAsync(int companyId, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetCompanyByIdAsync(companyId, cancellationToken);
        if (company?.ItemMappingEnabled != true)
        {
            return new ItemMappingContextDto();
        }

        var mappings = await itemMappingRepository.GetActiveByCompanyAsync(companyId, cancellationToken);
        return new ItemMappingContextDto
        {
            IsEnabled = true,
            Mappings = mappings.Select(mapping => new ItemMappingDto
            {
                Id = mapping.Id,
                CompanyId = mapping.CompanyId,
                GmkItemId = mapping.GmkItemId,
                GmkItemName = mapping.GmkItemName,
                DistributorItemId = mapping.DistributorItemId,
                DistributorItemName = mapping.DistributorItemName,
                IsActive = mapping.IsActive
            }).ToList()
        };
    }

    public ItemIdentityResolutionDto Resolve(ItemMappingContextDto context, string? distributorItemId,
        string? distributorItemName)
    {
        var id = Trim(distributorItemId);
        var name = Trim(distributorItemName);
        if (!context.IsEnabled)
        {
            return new ItemIdentityResolutionDto
            {
                DistributorItemId = id,
                DistributorItemName = name,
                GmkItemId = id,
                GmkItemName = name
            };
        }

        var mapping = context.FindActive(id);
        if (mapping is null)
        {
            return new ItemIdentityResolutionDto
            {
                DistributorItemId = id,
                DistributorItemName = name,
                ErrorReason = $"Item ID distributor \"{id}\" belum punya item mapping GMK yang aktif. Minta admin PT. GMK menambahkan mapping item ini."
            };
        }

        return new ItemIdentityResolutionDto
        {
            DistributorItemId = id,
            DistributorItemName = name,
            GmkItemId = mapping.GmkItemId,
            GmkItemName = mapping.GmkItemName,
            ItemMappingId = mapping.Id
        };
    }

    private static string? Trim(string? value) => value?.Trim();
}
