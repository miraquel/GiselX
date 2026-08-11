using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IDistributorTemplateMappingService
{
    Task<ServiceResponse<IReadOnlyList<DistributorTemplateMappingDto>>> ListAsync(int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<DistributorTemplateMappingDto?>> GetAsync(int id, int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<int>> SaveAsync(SaveDistributorTemplateMappingDto mapping, string username,
        CancellationToken cancellationToken);
    Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
        CancellationToken cancellationToken);
    Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken cancellationToken);
}
