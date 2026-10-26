using GiselX.Common.Constants;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

/// <summary>One page naming every company whose uploads contain distributor items nobody has mapped yet.</summary>
[Area("Admin")]
[Authorize(PermissionConstants.ItemMappings.Index)]
public sealed class UnmappedItemsController(IItemMappingService mappingService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await mappingService.ListUnmappedSummaryAsync(cancellationToken);
        return View(response.Data ?? []);
    }
}
