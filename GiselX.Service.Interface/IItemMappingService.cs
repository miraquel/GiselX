using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IItemMappingService
{
    Task<ServiceResponse<IReadOnlyList<ItemMappingDto>>> ListAsync(int companyId, CancellationToken token);
    Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(SaveItemMappingDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token);
}
