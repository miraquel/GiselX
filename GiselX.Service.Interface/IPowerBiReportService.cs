using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IPowerBiReportService
{
    Task<ServiceResponse<IReadOnlyList<PowerBiReportDto>>> ListAdminAsync(int? companyId, CancellationToken token);
    Task<ServiceResponse<PowerBiReportDto?>> GetAdminAsync(int id, CancellationToken token);
    Task<ServiceResponse<IReadOnlyList<CompanyOptionDto>>> ListCompanyOptionsAsync(CancellationToken token);
    Task<ServiceResponse<int>> GetNextDisplayOrderAsync(int companyId, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(SavePowerBiReportDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, CancellationToken token);
    Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token);
}
