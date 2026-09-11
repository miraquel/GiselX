using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IBusinessUnitRepository
{
    Task<IReadOnlyList<BusinessUnit>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<BusinessUnit?> GetAsync(int id, CancellationToken cancellationToken);
    Task<int> InsertAsync(BusinessUnit businessUnit, CancellationToken cancellationToken);
    Task UpdateAsync(BusinessUnit businessUnit, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);
    Task<BusinessUnitDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
}
