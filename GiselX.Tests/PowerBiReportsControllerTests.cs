using System.Reflection;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class PowerBiReportsControllerTests
{
    [Fact]
    public void Controller_requires_authenticated_user()
    {
        Assert.Contains(typeof(PowerBiReportsController).GetCustomAttributes(true),
            value => value is AuthorizeAttribute attribute && string.IsNullOrEmpty(attribute.Roles));
    }

    [Fact]
    public void View_does_not_accept_a_company_id()
    {
        var parameters = typeof(PowerBiReportsController).GetMethod(nameof(PowerBiReportsController.View),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!.GetParameters();

        Assert.Collection(parameters,
            parameter => Assert.Equal(typeof(int), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }

    [Fact]
    public async Task View_returns_not_found_for_missing_inactive_or_other_company_report()
    {
        var controller = new PowerBiReportsController(new ViewerService(data: null, statusCode: 404));

        Assert.IsType<NotFoundResult>(await controller.View(42, default));
    }

    [Fact]
    public async Task View_renders_company_scoped_viewer_model()
    {
        var dto = new PowerBiReportViewerDto(42, "Sales", "https://app.powerbi.com/view?r=abc");
        var service = new ViewerService(dto, 200);
        var token = new CancellationToken(canceled: true);

        var result = Assert.IsType<ViewResult>(await new PowerBiReportsController(service).View(42, token));

        Assert.Same(dto, result.Model);
        Assert.Equal(42, service.ViewerId);
        Assert.Equal(token, service.ViewerToken);
    }

    private sealed class ViewerService(PowerBiReportViewerDto? data, int statusCode) : IPowerBiReportService
    {
        public int? ViewerId { get; private set; }
        public CancellationToken ViewerToken { get; private set; }

        public Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token)
        {
            ViewerId = id;
            ViewerToken = token;
            return Task.FromResult(new ServiceResponse<PowerBiReportViewerDto?> { Data = data, StatusCode = statusCode });
        }

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportDto>>> ListAdminAsync(int? companyId,
            CancellationToken token) => throw new NotSupportedException();

        public Task<ServiceResponse<PowerBiReportDto?>> GetAdminAsync(int id, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<IReadOnlyList<CompanyOptionDto>>> ListCompanyOptionsAsync(CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<int>> GetNextDisplayOrderAsync(int companyId, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<int>> SaveAsync(SavePowerBiReportDto dto, string username, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse> DeleteAsync(int id, CancellationToken token) => throw new NotSupportedException();

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token) =>
            throw new NotSupportedException();
    }
}
