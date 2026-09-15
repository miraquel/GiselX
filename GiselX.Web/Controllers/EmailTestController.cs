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

        // Parsed the same way a company's ContactEmail is, so this screen can be used to prove a
        // multi-address list actually delivers before it is saved against a company.
        var recipients = GiselX.Service.NotificationRecipients.Parse(toEmail);
        if (recipients.Count == 0)
        {
            TempData["ErrorMessage"] =
                $"'{toEmail}' contains no valid email address. Separate multiple addresses with ';'.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _emailService.SendReminderAsync(recipients, companyName, deadline, isFinalReminder);
            TempData["StatusMessage"] = $"Test email sent to {string.Join(", ", recipients)} successfully.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to send email: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
