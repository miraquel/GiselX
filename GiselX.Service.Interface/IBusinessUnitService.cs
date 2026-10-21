using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IBusinessUnitService
{
    Task<ServiceResponse<IReadOnlyList<BusinessUnitDto>>> ListAsync(bool activeOnly, CancellationToken token);
    Task<ServiceResponse<BusinessUnitDto?>> GetAsync(int id, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(BusinessUnitDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, CancellationToken token);
}
