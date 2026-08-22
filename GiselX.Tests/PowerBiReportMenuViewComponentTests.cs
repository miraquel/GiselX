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
using Microsoft.Extensions.Logging.Abstractions;

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
        var component = CreateComponent(MenuService.Returning(items));

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
        Assert.Equal(items, Assert.IsAssignableFrom<IReadOnlyList<PowerBiReportMenuItemDto>>(result.ViewData!.Model));
    }

    [Fact]
    public void Component_has_no_company_id_input()
    {
        var method = typeof(PowerBiReportMenuViewComponent).GetMethod(nameof(PowerBiReportMenuViewComponent.InvokeAsync))!;
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_without_calling_the_service_for_anonymous_users()
    {
        var service = MenuService.Throwing(new InvalidOperationException("service must not be called"));
        var component = CreateComponent(service, authenticated: false);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
        Assert.Equal(0, service.ListMenuCallCount);
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_when_the_service_response_is_unsuccessful()
    {
        var service = MenuService.Returning([], errors: ["sensitive service failure"]);
        var component = CreateComponent(service);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_when_the_service_throws()
    {
        var service = MenuService.Throwing(new InvalidOperationException("sensitive exception detail"));
        var component = CreateComponent(service);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
    }

    [Fact]
    public async Task InvokeAsync_rethrows_cancellation_when_the_request_is_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = MenuService.Throwing(new OperationCanceledException(cancellation.Token));
        var component = CreateComponent(service, requestAborted: cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => component.InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_for_an_unrelated_operation_cancellation()
    {
        var service = MenuService.Throwing(new OperationCanceledException("transient downstream cancellation"));
        var component = CreateComponent(service);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
    }

    [Fact]
    public async Task InvokeAsync_forwards_the_request_cancellation_token_to_the_service()
    {
        using var cancellation = new CancellationTokenSource();
        var service = MenuService.Returning([]);
        var component = CreateComponent(service, requestAborted: cancellation.Token);

        await component.InvokeAsync();

        Assert.Equal(cancellation.Token, service.ListMenuToken);
    }

    private static PowerBiReportMenuViewComponent CreateComponent(MenuService service, bool authenticated = true,
        CancellationToken requestAborted = default)
    {
        var component = new PowerBiReportMenuViewComponent(service,
            NullLogger<PowerBiReportMenuViewComponent>.Instance)
        {
            ViewComponentContext = ViewComponentContext(authenticated, requestAborted)
        };
        return component;
    }

    private static IReadOnlyList<PowerBiReportMenuItemDto> MenuModel(IViewComponentResult result) =>
        Assert.IsAssignableFrom<IReadOnlyList<PowerBiReportMenuItemDto>>(
            Assert.IsType<ViewViewComponentResult>(result).ViewData!.Model);

    private static ViewComponentContext ViewComponentContext(bool authenticated, CancellationToken requestAborted)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: authenticated ? "test" : null)),
            RequestAborted = requestAborted
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

    private sealed class MenuService(Func<CancellationToken,
        Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>>> listMenu) : IPowerBiReportService
    {
        public int ListMenuCallCount { get; private set; }
        public CancellationToken ListMenuToken { get; private set; }

        public static MenuService Returning(IReadOnlyList<PowerBiReportMenuItemDto> items, List<string>? errors = null) =>
            new(_ => Task.FromResult(new ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>
            {
                Data = items,
                Errors = errors ?? []
            }));

        public static MenuService Throwing(Exception exception) => new(_ => Task.FromException<
            ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>>(exception));

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token)
        {
            ListMenuCallCount++;
            ListMenuToken = token;
            return listMenu(token);
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

        public Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
