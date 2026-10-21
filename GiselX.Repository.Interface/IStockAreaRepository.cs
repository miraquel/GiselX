using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IStockAreaRepository
{
    Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly, CancellationToken cancellationToken);
    Task<StockArea?> GetAsync(int id, int companyId, CancellationToken cancellationToken);
    Task<int> InsertAsync(StockArea area, CancellationToken cancellationToken);
    Task UpdateAsync(StockArea area, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);

    /// <summary>Returns false when Stock or StockStaging still references the area (or it does not exist).</summary>
    Task<bool> DeleteAsync(int id, int companyId, CancellationToken cancellationToken);
}
