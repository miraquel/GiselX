using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Web.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        return View();
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