using GiselX.Common.Constants;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    // [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Index)]
    public async Task<IActionResult> Get([FromQuery] PagedListRequestDto listRequest, CancellationToken cancellationToken)
    {
        var response = await _companyService.GetCompaniesAsync(listRequest, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:int}")]
    // [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Details)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var response = await _companyService.GetCompanyByIdAsync(id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}