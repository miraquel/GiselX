using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IStockService
{
    Task<ServiceResponse> UploadAsync(IEnumerable<StockDto> stocks, int companyId, CancellationToken cancellationToken);
    Task<ServiceResponse<PagedListDto<StockDto>>> SelectAsync(PagedListRequestDto pagedListRequest, CancellationToken cancellationToken);
    Task<ServiceResponse<IEnumerable<StockDto>>> SelectByCustPeriodAsync(int companyId, int year, int month, CancellationToken cancellationToken);
    Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken);
}
