using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IStockAreaService
{
    Task<ServiceResponse<IReadOnlyList<StockAreaDto>>> ListAsync(int companyId, CancellationToken token);
    Task<ServiceResponse<StockAreaDto?>> GetAsync(int id, int companyId, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(StockAreaDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token);
}
