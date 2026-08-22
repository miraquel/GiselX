using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GiselX.Web.Controllers;

public sealed class PowerBiReportMenuViewComponent(IPowerBiReportMenuService service,
    ILogger<PowerBiReportMenuViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true)
            return View(Array.Empty<PowerBiReportMenuItemDto>());

        try
        {
            var response = await service.ListMenuAsync(HttpContext.RequestAborted);
            if (!response.IsSuccess)
            {
                logger.LogWarning("Power BI report navigation is unavailable.");
                return View(Array.Empty<PowerBiReportMenuItemDto>());
            }

            return View(response.Data ?? []);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Power BI report navigation is unavailable.");
            return View(Array.Empty<PowerBiReportMenuItemDto>());
        }
    }
}
