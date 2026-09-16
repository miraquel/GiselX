using GiselX.Common.Constants;
using GiselX.Service;
using GiselX.Service.Dto;
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

    /// <summary>
    /// Sends a sample upload confirmation. Without this the only way to see that mail was to perform
    /// a real upload, which is a poor way to check an SMTP change. The figures are illustrative and
    /// the subject says so, so a test message is never mistaken for a real upload report.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendUploadNotification(
        string toEmail,
        string companyName,
        UploadNotificationKind kind,
        bool withRejectedRows,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || string.IsNullOrWhiteSpace(companyName))
        {
            TempData["ErrorMessage"] = "Email and company name are required.";
            return RedirectToAction(nameof(Index));
        }

        var recipients = GiselX.Service.NotificationRecipients.Parse(toEmail);
        if (recipients.Count == 0)
        {
            TempData["ErrorMessage"] =
                $"'{toEmail}' contains no valid email address. Separate multiple addresses with ';'.";
            return RedirectToAction(nameof(Index));
        }

        var total = 320;
        var invalid = withRejectedRows ? 8 : 0;
        var notification = new UploadNotificationDto
        {
            CompanyId = 0,
            Kind = kind,
            // No batch exists behind a sample, so no reference and no result link that would 404.
            Reference = null,
            TotalRows = total,
            ValidRows = total - invalid,
            InvalidRows = invalid,
            UploadedBy = User.Identity?.Name ?? "email-test",
            CompletedAtUtc = DateTime.UtcNow,
            OriginalFileName = kind == UploadNotificationKind.Stock
                ? "contoh-stock.xlsx"
                : "contoh-sellout.xlsx",
            PeriodLabel = kind switch
            {
                UploadNotificationKind.Stock =>
                    $"Snapshot {DateTime.Today.ToString("dd MMMM yyyy", UploadNotificationMessage.Culture)}",
                UploadNotificationKind.Sellout =>
                    $"Minggu {DateTime.Today.ToString("dd MMMM yyyy", UploadNotificationMessage.Culture)}",
                _ => null
            },
            OutletCount = kind == UploadNotificationKind.Sellout ? 87 : null,
            ProductCount = total - invalid
        };

        try
        {
            await _emailService.SendUploadCompletedAsync(
                notification, companyName, recipients, [], cancellationToken);
            TempData["StatusMessage"] =
                $"Sample {kind} upload notification sent to {string.Join(", ", recipients)}.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to send email: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
