using GiselX.Common.Constants;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers.Api;

[Route("api/[controller]/[action]")]
[ApiController]
public class StocksController : ControllerBase
{
    private readonly IStockService _stockService;

    public StocksController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [HttpGet]
    [Authorize(PermissionConstants.Stocks.Index)]
    public async Task<IActionResult> SelectAsync([FromQuery] PagedListRequestDto listRequest,
        CancellationToken cancellationToken)
    {
        var response = await _stockService.SelectAsync(listRequest, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    [Authorize(PermissionConstants.Stocks.Index)]
    public async Task<IActionResult> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var response = await _stockService.SelectPeriodsAsync(cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
