using GiselX.Common.Constants;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
[Authorize(PermissionConstants.EmailTest.Send)]
public class EmailTestController : Controller
{
    private readonly IEmailService _emailService;
    private readonly ILatestUploadRepository _latestUploads;
    private readonly UserManager<AppIdentityUser> _userManager;

    public EmailTestController(IEmailService emailService, ILatestUploadRepository latestUploads,
        UserManager<AppIdentityUser> userManager)
    {
        _emailService = emailService;
        _latestUploads = latestUploads;
        _userManager = userManager;
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

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        // Sent against the company's most recent real upload when there is one, so the result and
        // download links in the message are live and can actually be clicked. Without this the
        // sample carried no links at all — which made the screen unable to check the one thing
        // people most want to check about a notification.
        // An Administrator looks across companies, as they do everywhere else here: GMK staff sit in
        // their own company, which has no distributor uploads, so scoping them to it would mean the
        // sample never found anything real. A distributor stays scoped to their own company.
        int? scope = User.IsInRole("Administrator") ? null : user.CompanyId;

        var latest = kind switch
        {
            UploadNotificationKind.Stock =>
                await _latestUploads.GetLatestStockAsync(scope, cancellationToken),
            UploadNotificationKind.Sellout =>
                await _latestUploads.GetLatestSelloutAsync(scope, cancellationToken),
            _ => null
        };

        var notification = latest is null
            ? BuildIllustrativeSample(kind, withRejectedRows)
            : BuildFromRealUpload(kind, latest);

        try
        {
            await _emailService.SendUploadCompletedAsync(
                notification, companyName, recipients, [], cancellationToken);

            // Says which of the two it sent, and why a link is missing when one is — otherwise an
            // absent download link looks like a fault rather than an upload made before archiving
            // was switched on.
            TempData["StatusMessage"] = latest is null
                ? $"Sample {kind} notification sent to {string.Join(", ", recipients)}. " +
                  "It uses illustrative figures and carries no links, because this company has no " +
                  $"completed {kind} upload to point at yet."
                : $"{kind} notification sent to {string.Join(", ", recipients)}, based on the real " +
                  $"upload of '{notification.OriginalFileName}'. " +
                  (notification.SourceFileArchived
                      ? "It includes a link to download that file."
                      : "It has no file-download link: that upload was made before the archive was " +
                        "configured, so the workbook itself was not kept.");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to send email: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// A sample against the company's real upload, so the result link and — when the workbook was
    /// archived — the download link both work when clicked.
    /// </summary>
    private static UploadNotificationDto BuildFromRealUpload(UploadNotificationKind kind,
        LatestUploadSummary latest) => new()
    {
        Kind = kind,
        Reference = latest.Reference,
        TotalRows = latest.TotalRows,
        ValidRows = latest.ValidRows,
        InvalidRows = latest.InvalidRows,
        UploadedBy = latest.UploadedBy,
        CompletedAtUtc = latest.CompletedDate ?? DateTime.UtcNow,
        OriginalFileName = latest.OriginalFileName,
        PeriodLabel = latest.StockDate is { } date
            ? $"Snapshot {date.ToString("dd MMMM yyyy", UploadNotificationMessage.Culture)}"
            : null,
        ProductCount = null,
        SourceFileArchived = !string.IsNullOrWhiteSpace(latest.ArchivedFilePath)
    };

    /// <summary>
    /// Used only when the company has never completed an upload of this kind. It carries no
    /// reference, so the message offers no links rather than links that would 404.
    /// </summary>
    private UploadNotificationDto BuildIllustrativeSample(UploadNotificationKind kind, bool withRejectedRows)
    {
        const int total = 320;
        var invalid = withRejectedRows ? 8 : 0;
        return new UploadNotificationDto
        {
            Kind = kind,
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
    }
}
