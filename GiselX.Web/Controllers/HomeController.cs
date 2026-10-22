using System.Diagnostics;
using GiselX.Domain;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Mvc;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace GiselX.Web.Controllers;

public class HomeController(
    IUploadDeadlineStatusService deadlineStatus,
    UserManager<AppIdentityUser> userManager) : Controller
{
    [Authorize]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Only a company with a deadline schedule gets the status card - in practice the distributors;
        // PT. GMK's own company has none, so administrators see the page as before.
        var user = await userManager.GetUserAsync(User);
        var status = user is null ? null : await deadlineStatus.GetAsync(user.CompanyId, cancellationToken);
        return View(new HomeIndexViewModel { DeadlineStatus = status });
    }

    [Authorize]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
