using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
[Authorize(PermissionConstants.Companies.Edit)]
public sealed class ChannelsController(IChannelService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View((await service.ListAsync(false, cancellationToken)).Data ?? []);

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Edit", new ChannelDto
        {
            Code = (await service.GetNextCodeAsync(cancellationToken)).Data ?? string.Empty
        });

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var value = (await service.GetAsync(id, cancellationToken)).Data;
        return value is null ? NotFound() : View(value);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ChannelDto model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Edit", model);
        var response = await service.SaveAsync(model, Username, cancellationToken);
        if (!response.IsSuccess)
        {
            foreach (var error in response.Errors) ModelState.AddModelError(string.Empty, error);
            return View("Edit", model);
        }
        TempData["StatusMessage"] = response.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken) =>
        RedirectToList(await service.SetActiveAsync(id, true, Username, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        RedirectToList(await service.SetActiveAsync(id, false, Username, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        RedirectToList(await service.DeleteAsync(id, cancellationToken));

    private string Username => User.Identity?.Name ?? "unknown";

    private IActionResult RedirectToList(ServiceResponse response)
    {
        if (response.IsSuccess) TempData["StatusMessage"] = response.Message;
        else TempData["ErrorMessage"] = string.Join(" ", response.Errors);
        return RedirectToAction(nameof(Index));
    }
}
