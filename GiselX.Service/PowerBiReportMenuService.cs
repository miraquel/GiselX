using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Identity;

namespace GiselX.Service;

public sealed class PowerBiReportMenuService(
    IPowerBiReportMenuRepository repository,
    UserClaimDto userClaimDto,
    UserManager<AppIdentityUser> userManager) : IPowerBiReportMenuService
{
    public async Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(
        CancellationToken token)
    {
        var user = await userManager.FindByNameAsync(userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");
        var reports = await repository.ListActiveByCompanyAsync(user.CompanyId, token);
        return new ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>
        {
            Data = reports.Select(report => new PowerBiReportMenuItemDto(report.Id, report.Name)).ToList(),
            Message = "Power BI report menu retrieved successfully."
        };
    }
}
