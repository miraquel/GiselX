using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IServiceLevelService
{
    Task<ServiceResponse> UploadAsync(IEnumerable<ServiceLevelDto> serviceLevels, CancellationToken cancellationToken);
    Task<ServiceResponse<PagedListDto<ServiceLevelDto>>> SelectAsync(PagedListRequestDto pagedListRequest,
        CancellationToken cancellationToken);
    Task<ServiceResponse<IEnumerable<ServiceLevelDto>>> SelectByCustPeriodAsync(string custCode, int year, int month,
        CancellationToken cancellationToken);
    Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken);
}