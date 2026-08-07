using GiselX.Common.Constants;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
[Authorize(PermissionConstants.EmailTest.Send)]
public class EmailTestController : Controller
{
    private readonly IEmailService _emailService;

    public EmailTestController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.ToEmail = User.Identity?.Name ?? string.Empty;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        string toEmail,
        string companyName,
        DateTime deadline,
        bool isFinalReminder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || string.IsNullOrWhiteSpace(companyName))
        {
            TempData["ErrorMessage"] = "Email and company name are required.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _emailService.SendReminderAsync(toEmail, companyName, deadline, isFinalReminder);
            TempData["StatusMessage"] = $"Test email sent to {toEmail} successfully.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to send email: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
