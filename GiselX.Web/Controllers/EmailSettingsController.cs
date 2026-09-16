using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

/// <summary>
/// Lets an operator configure SMTP without a deployment. The values used to live only in
/// appsettings.json, which a copy-paste deployment overwrites — so fixing mail meant editing a file
/// on the server and restarting, and getting it wrong took the application down with it.
/// </summary>
[Area("Admin")]
[Authorize(PermissionConstants.EmailSettings.Manage)]
public sealed class EmailSettingsController(IEmailSettingService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(cancellationToken);
        if (response.Data?.IsFallbackFromConfiguration == true) TempData["InfoMessage"] = response.Message;
        return View(response.Data ?? new EmailSettingDto());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EmailSettingDto model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Index", model);

        var username = User.Identity?.Name ?? "unknown";
        var response = await service.SaveAsync(model, username, cancellationToken);
        if (!response.IsSuccess)
        {
            foreach (var error in response.Errors) ModelState.AddModelError(string.Empty, error);
            // Re-read so the form still knows whether a password is stored; the posted model does
            // not carry it, and losing that flag would make the field look empty-by-default.
            model.HasStoredPassword = (await service.GetAsync(cancellationToken)).Data?.HasStoredPassword ?? false;
            return View("Index", model);
        }

        TempData["StatusMessage"] = response.Message;
        return RedirectToAction(nameof(Index));
    }
}
