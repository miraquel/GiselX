using GiselX.Common.Constants;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers.Api;

[Route("api/[controller]/[action]")]
[ApiController]
public class ServiceLevelsController : ControllerBase
{
    private readonly IServiceLevelService _serviceLevelService;

    public ServiceLevelsController(IServiceLevelService serviceLevelService)
    {
        _serviceLevelService = serviceLevelService;
    }

    [HttpGet]
    // [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.ServiceLevels.Index)]
    public async Task<IActionResult> SelectAsync([FromQuery] PagedListRequestDto listRequest,
        CancellationToken cancellationToken)
    {
        var response = await _serviceLevelService.SelectAsync(listRequest, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
    
    [HttpGet]
    // [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.ServiceLevels.Index)]
    public async Task<IActionResult> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var response = await _serviceLevelService.SelectPeriodsAsync(cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}