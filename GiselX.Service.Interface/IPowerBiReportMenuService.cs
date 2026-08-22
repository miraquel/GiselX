using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IPowerBiReportMenuService
{
    Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token);
}
