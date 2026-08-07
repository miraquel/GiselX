using GiselX.Domain;
using GiselX.Domain.Common;

namespace GiselX.Repository.Interface;

public interface ISalesTransactionRepository
{
    Task UploadAsync(IEnumerable<SalesTransaction> salesTransactions, CancellationToken cancellationToken);
    Task<IEnumerable<SalesTransaction>> SelectByCustAsync(int companyId, CancellationToken cancellationToken);
    Task<IEnumerable<SalesTransaction>> SelectByCustPeriodAsync(int companyId, int year, int month, CancellationToken cancellationToken);
    Task DeleteAsync(SalesTransaction salesTransaction, CancellationToken cancellationToken);
    Task<PagedList<SalesTransaction>> SelectAsync(PagedListRequest pagedListRequest, int companyId, CancellationToken cancellationToken);
    Task<IEnumerable<Period>> SelectPeriodsAsync(int companyId, CancellationToken cancellationToken);
}
