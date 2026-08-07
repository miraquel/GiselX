using GiselX.Domain;
using GiselX.Domain.Common;

namespace GiselX.Repository.Interface;

public interface IStockRepository
{
    Task UploadAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken);
    Task<IEnumerable<Stock>> SelectByCustAsync(int companyId, CancellationToken cancellationToken);
    Task<IEnumerable<Stock>> SelectByCustPeriodAsync(int companyId, int year, int month, CancellationToken cancellationToken);
    Task<PagedList<Stock>> SelectAsync(PagedListRequest pagedListRequest, int companyId, CancellationToken cancellationToken);
    Task<IEnumerable<Period>> SelectPeriodsAsync(int companyId, CancellationToken cancellationToken);
}
