using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface ISalesmanRepository
{
    Task<IReadOnlyList<Salesman>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<Salesman?> GetAsync(int id, CancellationToken cancellationToken);
    Task<int> InsertAsync(Salesman salesman, CancellationToken cancellationToken);
    Task UpdateAsync(Salesman salesman, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);
    Task<SalesmanDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
}
