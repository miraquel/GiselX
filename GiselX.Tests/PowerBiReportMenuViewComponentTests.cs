using System.Security.Claims;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GiselX.Tests;

public sealed class PowerBiReportMenuViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_passes_only_service_menu_items_to_the_view()
    {
        var items = new[]
        {
            new PowerBiReportMenuItemDto(2, "Stock Dashboard"),
            new PowerBiReportMenuItemDto(3, "Sales Dashboard")
        };
        var component = new PowerBiReportMenuViewComponent(new MenuService(items));
        component.ViewComponentContext = AuthenticatedViewComponentContext();

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
        Assert.Equal(items, Assert.IsAssignableFrom<IReadOnlyList<PowerBiReportMenuItemDto>>(result.ViewData!.Model));
    }

    [Fact]
    public void Component_has_no_company_id_input()
    {
        var method = typeof(PowerBiReportMenuViewComponent).GetMethod(nameof(PowerBiReportMenuViewComponent.InvokeAsync))!;
        Assert.Empty(method.GetParameters());
    }

    private static ViewComponentContext AuthenticatedViewComponentContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"))
        };
        return new ViewComponentContext
        {
            ViewContext = new ViewContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            }
        };
    }

    private sealed class MenuService(IReadOnlyList<PowerBiReportMenuItemDto> items) : IPowerBiReportService
    {
        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token) =>
            Task.FromResult(new ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>> { Data = items });

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

        public Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
