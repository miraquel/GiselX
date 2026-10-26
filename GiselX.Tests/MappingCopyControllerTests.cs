using System.Security.Claims;
using GiselX.Common.Constants;
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

public sealed class MappingCopyControllerTests
{
    [Fact]
    public async Task Copy_drops_sections_the_user_may_not_manage()
    {
        var service = new CapturingCopyService();
        var controller = CreateController(service, PermissionConstants.StockTemplateMappings.Manage);

        await controller.Copy(2, 1, [10], [11], true, CancellationToken.None);

        var request = Assert.IsType<MappingCopyRequestDto>(service.Copied);
        Assert.Empty(request.SelloutMappingIds);
        Assert.Equal([11], request.StockMappingIds);
        Assert.False(request.CopyItemMappings);
        Assert.Equal("admin", service.CopiedBy);
    }

    [Fact]
    public async Task Copy_shows_the_result_after_a_successful_copy()
    {
        var service = new CapturingCopyService();
        var controller = CreateController(service, PermissionConstants.DistributorTemplateMappings.Manage);

        var result = await controller.Copy(2, 1, [10], null, false, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Result", view.ViewName);
        Assert.IsType<MappingCopyResultViewModel>(view.Model);
    }

    [Fact]
    public async Task Copy_returns_to_the_form_with_the_error_when_the_service_refuses()
    {
        var service = new CapturingCopyService
        {
            CopyResponse = new ServiceResponse<MappingCopyResultDto>
            {
                Errors = ["Choose a different company to copy from."], StatusCode = 400
            }
        };
        var controller = CreateController(service, PermissionConstants.DistributorTemplateMappings.Manage);

        var result = await controller.Copy(2, 2, [10], null, false, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MappingCopyController.Index), redirect.ActionName);
        Assert.Equal("Choose a different company to copy from.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Index_without_source_lists_sources_and_has_no_preview()
    {
        var service = new CapturingCopyService();
        var controller = CreateController(service);

        var result = await controller.Index(2, null, CancellationToken.None);

        var model = Assert.IsType<MappingCopyViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.Preview);
        Assert.Single(model.Sources);
        Assert.Equal("PT ABC – Medan", model.TargetCompanyName);
        Assert.Null(service.PreviewedSource);
    }

    [Fact]
    public async Task Index_with_source_previews_and_reports_section_permissions()
    {
        var service = new CapturingCopyService();
        var controller = CreateController(service, PermissionConstants.ItemMappings.Manage);

        var result = await controller.Index(2, 1, CancellationToken.None);

        var model = Assert.IsType<MappingCopyViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.NotNull(model.Preview);
        Assert.Equal(1, service.PreviewedSource);
        Assert.False(model.CanCopySellout);
        Assert.False(model.CanCopyStock);
        Assert.True(model.CanCopyItems);
    }

    [Fact]
    public async Task Index_returns_not_found_for_an_unknown_target()
    {
        var controller = CreateController(new CapturingCopyService());

        var result = await controller.Index(99, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static MappingCopyController CreateController(CapturingCopyService service, params string[] allowedPolicies)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "TestAuthentication"))
        };
        return new MappingCopyController(service, new PolicyListAuthorizationService(allowedPolicies))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider())
        };
    }

    private sealed class CapturingCopyService : IMappingCopyService
    {
        public int? PreviewedSource { get; private set; }
        public MappingCopyRequestDto? Copied { get; private set; }
        public string? CopiedBy { get; private set; }
        public ServiceResponse<MappingCopyResultDto> CopyResponse { get; init; } =
            new() { Data = new MappingCopyResultDto { SelloutCopied = 1 }, Message = "Mappings copied." };

        public Task<CopySourceCompanyDto?> GetCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(companyId == 2 ? new CopySourceCompanyDto { Id = 2, DisplayName = "PT ABC – Medan" } : null);

        public Task<IReadOnlyList<CopySourceCompanyDto>> ListSourceCompaniesAsync(int targetCompanyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CopySourceCompanyDto>>(
                [new CopySourceCompanyDto { Id = 1, DisplayName = "PT ABC – Jakarta" }]);

        public Task<ServiceResponse<MappingCopyPreviewDto>> PreviewAsync(int sourceCompanyId, int targetCompanyId,
            CancellationToken cancellationToken)
        {
            PreviewedSource = sourceCompanyId;
            return Task.FromResult(new ServiceResponse<MappingCopyPreviewDto>
            {
                Data = new MappingCopyPreviewDto { SourceCompanyId = sourceCompanyId, TargetCompanyId = targetCompanyId }
            });
        }

        public Task<ServiceResponse<MappingCopyResultDto>> CopyAsync(MappingCopyRequestDto request, string username,
            CancellationToken cancellationToken)
        {
            Copied = request;
            CopiedBy = username;
            return Task.FromResult(CopyResponse);
        }
    }

    private sealed class PolicyListAuthorizationService(IEnumerable<string> allowed) : IAuthorizationService
    {
        private readonly HashSet<string> _allowed = allowed.ToHashSet(StringComparer.Ordinal);

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(_allowed.Contains(policyName) ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) => throw new NotSupportedException();
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
