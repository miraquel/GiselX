using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class CompaniesControllerTests
{
    [Fact]
    public async Task Edit_shows_service_errors_when_mapping_enablement_is_rejected()
    {
        var controller = new CompaniesController(new RejectingCompanyService(
            "At least one active item mapping is required before item mapping can be enabled."));
        var model = new CompanyDto { Id = 7, Name = "Distributor", ItemMappingEnabled = true };

        var result = await controller.Edit(7, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        var errors = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("At least one active item mapping is required before item mapping can be enabled.",
            errors.ErrorMessage);
    }

    [Fact]
    public async Task Edit_shows_a_fallback_when_a_failed_service_response_has_no_message_or_errors()
    {
        var controller = new CompaniesController(new RejectingCompanyService());
        var model = new CompanyDto { Id = 7, Name = "Distributor", ItemMappingEnabled = true };

        var result = await controller.Edit(7, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Equal("Unable to update the company. Please try again.", error.ErrorMessage);
    }

    private sealed class RejectingCompanyService(params string[] errors) : ICompanyService
    {
        public Task<ServiceResponse<CompanyDto>> CreateCompanyAsync(CompanyDto companyDto,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResponse<CompanyDto?>> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<PagedListDto<CompanyDto>>> GetCompaniesAsync(PagedListRequestDto pagedListRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResponse<CompanyDto>> UpdateCompanyAsync(CompanyDto companyDto,
            CancellationToken cancellationToken) => Task.FromResult(new ServiceResponse<CompanyDto>
        {
            Errors = [.. errors],
            StatusCode = StatusCodes.Status400BadRequest
        });
    }
}
