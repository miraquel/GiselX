using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IItemMappingRepository
{
    Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken);
    Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken);
    Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);
    Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken);
    Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken);
}
