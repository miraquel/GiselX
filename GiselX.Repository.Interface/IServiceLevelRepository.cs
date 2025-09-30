using GiselX.Domain;
using GiselX.Domain.Common;

namespace GiselX.Repository.Interface;

public interface IServiceLevelRepository
{
    Task UploadAsync(IEnumerable<ServiceLevel> serviceLevels, CancellationToken cancellationToken);
    Task<IEnumerable<ServiceLevel>> SelectByCustAsync(string custCode, CancellationToken cancellationToken);
    Task<IEnumerable<ServiceLevel>> SelectByCustPeriodAsync(string custCode, int year, int month, CancellationToken cancellationToken);
    Task DeleteAsync(ServiceLevel serviceLevel, CancellationToken cancellationToken);
    Task<PagedList<ServiceLevel>> SelectAsync(PagedListRequest pagedListRequest, string custCode,
        CancellationToken cancellationToken);
    Task<IEnumerable<Period>> SelectPeriodsAsync(string custCode, CancellationToken cancellationToken);
}