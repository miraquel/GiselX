using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

public sealed class PowerBiReportMenuViewComponent(IPowerBiReportService service) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true)
            return View(Array.Empty<PowerBiReportMenuItemDto>());

        var response = await service.ListMenuAsync(HttpContext.RequestAborted);
        return View(response.Data ?? []);
    }
}
