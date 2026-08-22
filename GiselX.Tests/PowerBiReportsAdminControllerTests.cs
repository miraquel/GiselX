using System.Reflection;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class PowerBiReportsAdminControllerTests
{
    [Fact]
    public void Controller_requires_administrator_role()
    {
        var attribute = Assert.Single(typeof(PowerBiReportsAdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("Administrator", attribute.Roles);
    }

    [Fact]
    public async Task Save_invalid_model_redisplays_form_with_company_options()
    {
        var service = new FakePowerBiReportService
        {
            CompanyOptions = [new CompanyOptionDto(7, "Distributor")]
        };
        var controller = new PowerBiReportsAdminController(service);
        controller.ModelState.AddModelError(nameof(PowerBiReportEditViewModel.Name), "Name is required.");
        var model = new PowerBiReportEditViewModel { CompanyId = 7 };

        var result = Assert.IsType<ViewResult>(await controller.Save(model, default));

        Assert.Equal("Edit", result.ViewName);
        Assert.Single(Assert.IsType<PowerBiReportEditViewModel>(result.Model).Companies);
        Assert.False(service.SaveCalled);
    }

    [Fact]
    public async Task Save_service_error_adds_model_error_and_does_not_redirect()
    {
        var service = new FakePowerBiReportService
        {
            CompanyOptions = [new CompanyOptionDto(7, "Distributor")],
            SaveResponse = new ServiceResponse<int>
            {
                StatusCode = 400,
                Errors = ["A report with this name already exists for the company."]
            }
        };
        var controller = new PowerBiReportsAdminController(service);
        var model = new PowerBiReportEditViewModel
        {
            CompanyId = 7,
            Name = "Sales",
            EmbedUrl = "https://app.powerbi.com/view?r=abc"
        };

        var result = Assert.IsType<ViewResult>(await controller.Save(model, default));

        Assert.Equal("Edit", result.ViewName);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Save_is_post_only_and_validates_antiforgery()
    {
        var method = typeof(PowerBiReportsAdminController).GetMethod(nameof(PowerBiReportsAdminController.Save))!;

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Theory]
    [InlineData(nameof(PowerBiReportsAdminController.Activate))]
    [InlineData(nameof(PowerBiReportsAdminController.Deactivate))]
    [InlineData(nameof(PowerBiReportsAdminController.DeleteConfirmed))]
    public void State_changing_actions_are_post_only_and_validate_antiforgery(string methodName)
    {
        var method = typeof(PowerBiReportsAdminController).GetMethod(methodName)!;

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task Preview_uses_admin_lookup_instead_of_current_company_lookup()
    {
        var service = new FakePowerBiReportService
        {
            AdminReport = new PowerBiReportDto
            {
                Id = 9,
                CompanyId = 4,
                Name = "Other Company",
                EmbedUrl = "https://app.powerbi.com/view?r=abc"
            }
        };
        var controller = new PowerBiReportsAdminController(service);

        var result = Assert.IsType<ViewResult>(await controller.Preview(9, default));

        Assert.Equal("~/Views/PowerBiReports/View.cshtml", result.ViewName);
        Assert.Equal(9, service.AdminLookupId);
        Assert.False(service.ViewerLookupCalled);
    }

    [Fact]
    public async Task Edit_returns_not_found_when_the_admin_report_is_missing()
    {
        var service = new FakePowerBiReportService();
        var controller = new PowerBiReportsAdminController(service);

        var result = await controller.Edit(404, default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, service.AdminLookupId);
    }

    private sealed class FakePowerBiReportService : IPowerBiReportService
    {
        public IReadOnlyList<CompanyOptionDto> CompanyOptions { get; init; } = [];
        public PowerBiReportDto? AdminReport { get; init; }
        public ServiceResponse<int> SaveResponse { get; init; } = new() { Data = 1 };
        public bool SaveCalled { get; private set; }
        public int? AdminLookupId { get; private set; }
        public bool ViewerLookupCalled { get; private set; }

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportDto>>> ListAdminAsync(int? companyId,
            CancellationToken token) => Task.FromResult(new ServiceResponse<IReadOnlyList<PowerBiReportDto>>
            {
                Data = []
            });

        public Task<ServiceResponse<PowerBiReportDto?>> GetAdminAsync(int id, CancellationToken token)
        {
            AdminLookupId = id;
            return Task.FromResult(new ServiceResponse<PowerBiReportDto?> { Data = AdminReport });
        }

        public Task<ServiceResponse<IReadOnlyList<CompanyOptionDto>>> ListCompanyOptionsAsync(CancellationToken token) =>
            Task.FromResult(new ServiceResponse<IReadOnlyList<CompanyOptionDto>> { Data = CompanyOptions });

        public Task<ServiceResponse<int>> GetNextDisplayOrderAsync(int companyId, CancellationToken token) =>
            Task.FromResult(new ServiceResponse<int> { Data = 0 });

        public Task<ServiceResponse<int>> SaveAsync(SavePowerBiReportDto dto, string username, CancellationToken token)
        {
            SaveCalled = true;
            return Task.FromResult(SaveResponse);
        }

        public Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username, CancellationToken token) =>
            Task.FromResult(new ServiceResponse());

        public Task<ServiceResponse> DeleteAsync(int id, CancellationToken token) => Task.FromResult(new ServiceResponse());

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token)
        {
            ViewerLookupCalled = true;
            throw new NotSupportedException();
        }
    }
}
