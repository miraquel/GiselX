using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface ISalesmanService
{
    Task<ServiceResponse<IReadOnlyList<SalesmanDto>>> ListAsync(bool activeOnly, CancellationToken token);
    Task<ServiceResponse<string>> GetNextCodeAsync(CancellationToken token);
    Task<ServiceResponse<SalesmanDto?>> GetAsync(int id, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(SalesmanDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, CancellationToken token);
}
