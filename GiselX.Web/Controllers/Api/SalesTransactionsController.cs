using GiselX.Common.Constants;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers.Api;

[Route("api/[controller]/[action]")]
[ApiController]
public class SalesTransactionsController : ControllerBase
{
    private readonly ISalesTransactionService _salesTransactionService;

    public SalesTransactionsController(ISalesTransactionService salesTransactionService)
    {
        _salesTransactionService = salesTransactionService;
    }

    [HttpGet]
    [Authorize(PermissionConstants.SalesTransactions.Index)]
    public async Task<IActionResult> SelectAsync([FromQuery] PagedListRequestDto listRequest,
        CancellationToken cancellationToken)
    {
        var response = await _salesTransactionService.SelectAsync(listRequest, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    [Authorize(PermissionConstants.SalesTransactions.Index)]
    public async Task<IActionResult> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var response = await _salesTransactionService.SelectPeriodsAsync(cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
