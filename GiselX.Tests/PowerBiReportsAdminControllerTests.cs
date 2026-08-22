using System.Reflection;
using System.Security.Claims;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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
    public async Task Save_existing_report_missing_returns_not_found_without_model_errors()
    {
        var service = new FakePowerBiReportService
        {
            SaveResponse = new ServiceResponse<int>
            {
                StatusCode = StatusCodes.Status404NotFound,
                Errors = ["The Power BI report was not found."]
            }
        };
        var controller = CreateController(service);

        var result = await controller.Save(EditModel(id: 19), default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(controller.ModelState);
        Assert.Equal(19, service.SaveDto!.Id);
    }

    [Fact]
    public async Task Save_create_forwards_the_full_dto_username_and_company_filter()
    {
        var service = new FakePowerBiReportService();
        var controller = CreateController(service);
        var model = EditModel(id: null);

        var result = await controller.Save(model, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PowerBiReportsAdminController.Index), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["companyId"]);
        Assert.Null(service.SaveDto!.Id);
        Assert.Equal(7, service.SaveDto.CompanyId);
        Assert.Equal("Sales", service.SaveDto.Name);
        Assert.Equal("https://app.powerbi.com/view?r=sales", service.SaveDto.EmbedUrl);
        Assert.Equal(12, service.SaveDto.DisplayOrder);
        Assert.False(service.SaveDto.IsActive);
        Assert.Equal("admin", service.SaveUsername);
    }

    [Fact]
    public async Task Save_edit_forwards_the_existing_id_and_redirects_to_its_company_filter()
    {
        var service = new FakePowerBiReportService();
        var controller = CreateController(service);
        var model = EditModel(id: 29);

        var result = await controller.Save(model, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PowerBiReportsAdminController.Index), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["companyId"]);
        Assert.Equal(29, service.SaveDto!.Id);
        Assert.Equal("admin", service.SaveUsername);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task State_change_forwards_id_state_username_and_company_filter(bool isActive)
    {
        var service = new FakePowerBiReportService();
        var controller = CreateController(service);

        var result = isActive
            ? await controller.Activate(23, 7, default)
            : await controller.Deactivate(23, 7, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PowerBiReportsAdminController.Index), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["companyId"]);
        Assert.Equal((23, isActive, "admin"), service.SetActiveCall);
    }

    [Fact]
    public async Task Activate_missing_report_returns_not_found_after_forwarding_the_exact_state_change()
    {
        var service = new FakePowerBiReportService
        {
            SetActiveResponse = new ServiceResponse { StatusCode = StatusCodes.Status404NotFound }
        };
        var controller = CreateController(service);

        var result = await controller.Activate(31, 7, default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal((31, true, "admin"), service.SetActiveCall);
    }

    [Fact]
    public async Task Delete_forwards_id_and_preserves_the_company_filter_on_redirect()
    {
        var service = new FakePowerBiReportService();
        var controller = CreateController(service);

        var result = await controller.DeleteConfirmed(37, 7, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PowerBiReportsAdminController.Index), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["companyId"]);
        Assert.Equal(37, service.DeletedId);
    }

    [Fact]
    public async Task Delete_missing_report_returns_not_found_after_forwarding_the_exact_id()
    {
        var service = new FakePowerBiReportService
        {
            DeleteResponse = new ServiceResponse { StatusCode = StatusCodes.Status404NotFound }
        };
        var controller = CreateController(service);

        var result = await controller.DeleteConfirmed(41, 7, default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(41, service.DeletedId);
    }

    [Fact]
    public async Task Delete_get_passes_the_company_filter_to_the_cancel_view_model()
    {
        var service = new FakePowerBiReportService
        {
            AdminReport = new PowerBiReportDto { Id = 43, CompanyId = 7, CompanyName = "Distributor", Name = "Sales" }
        };
        var controller = CreateController(service);

        var result = Assert.IsType<ViewResult>(await controller.Delete(43, 7, default));

        var model = Assert.IsType<PowerBiReportDeleteViewModel>(result.Model);
        Assert.Equal(7, model.CompanyId);
    }

    [Fact]
    public async Task Delete_unfiltered_flow_keeps_null_for_cancel_and_success_redirect()
    {
        var service = new FakePowerBiReportService
        {
            AdminReport = new PowerBiReportDto { Id = 47, CompanyId = 7, CompanyName = "Distributor", Name = "Sales" }
        };
        var controller = CreateController(service);

        var deleteView = Assert.IsType<ViewResult>(await controller.Delete(47, null, default));
        var deleteModel = Assert.IsType<PowerBiReportDeleteViewModel>(deleteView.Model);
        var deleteResult = await controller.DeleteConfirmed(47, null, default);

        Assert.Null(deleteModel.CompanyId);
        var redirect = Assert.IsType<RedirectToActionResult>(deleteResult);
        Assert.True(redirect.RouteValues!.TryGetValue("companyId", out var companyId));
        Assert.Null(companyId);
    }

    private static PowerBiReportEditViewModel EditModel(int? id) => new()
    {
        Id = id,
        CompanyId = 7,
        Name = "Sales",
        EmbedUrl = "https://app.powerbi.com/view?r=sales",
        DisplayOrder = 12,
        IsActive = false
    };

    private static PowerBiReportsAdminController CreateController(FakePowerBiReportService service)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin")], "TestAuthentication"))
        };
        return new PowerBiReportsAdminController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider())
        };
    }

    private sealed class FakePowerBiReportService : IPowerBiReportService
    {
        public IReadOnlyList<CompanyOptionDto> CompanyOptions { get; init; } = [];
        public PowerBiReportDto? AdminReport { get; init; }
        public ServiceResponse<int> SaveResponse { get; init; } = new() { Data = 1 };
        public ServiceResponse SetActiveResponse { get; init; } = new();
        public ServiceResponse DeleteResponse { get; init; } = new();
        public bool SaveCalled { get; private set; }
        public SavePowerBiReportDto? SaveDto { get; private set; }
        public string? SaveUsername { get; private set; }
        public (int Id, bool IsActive, string Username)? SetActiveCall { get; private set; }
        public int? DeletedId { get; private set; }
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
            SaveDto = dto;
            SaveUsername = username;
            return Task.FromResult(SaveResponse);
        }

        public Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username, CancellationToken token)
        {
            SetActiveCall = (id, isActive, username);
            return Task.FromResult(SetActiveResponse);
        }

        public Task<ServiceResponse> DeleteAsync(int id, CancellationToken token)
        {
            DeletedId = id;
            return Task.FromResult(DeleteResponse);
        }

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token)
        {
            ViewerLookupCalled = true;
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
