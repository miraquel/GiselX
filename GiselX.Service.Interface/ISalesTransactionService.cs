using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface ISalesTransactionService
{
    Task<ServiceResponse> UploadAsync(IEnumerable<SalesTransactionDto> salesTransactions, int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<PagedListDto<SalesTransactionDto>>> SelectAsync(PagedListRequestDto pagedListRequest,
        CancellationToken cancellationToken);
    Task<ServiceResponse<IEnumerable<SalesTransactionDto>>> SelectByCustPeriodAsync(int companyId, int year, int month,
        CancellationToken cancellationToken);
    Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken);
}
