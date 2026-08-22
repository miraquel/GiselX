using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Authorize]
public sealed class PowerBiReportsController(IPowerBiReportService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> View(int id, CancellationToken token)
    {
        var response = await service.GetViewerAsync(id, token);
        return response.Data is null ? NotFound() : View(response.Data);
    }
}
