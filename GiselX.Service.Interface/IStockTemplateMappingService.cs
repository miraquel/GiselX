using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IStockTemplateMappingService
{
    Task<ServiceResponse<IReadOnlyList<StockTemplateMappingDto>>> ListAsync(int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<StockTemplateMappingDto?>> GetAsync(int id, int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<int>> SaveAsync(SaveStockTemplateMappingDto mapping, string username,
        CancellationToken cancellationToken);
    Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
        CancellationToken cancellationToken);
    Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken cancellationToken);
}
