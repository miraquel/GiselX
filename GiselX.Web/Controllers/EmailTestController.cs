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
    private readonly IEmailSettingsProvider _emailSettings;
    private readonly IUploadArchive _archive;
    private readonly ILatestUploadRepository _latestUploads;
    private readonly UserManager<AppIdentityUser> _userManager;

    public EmailTestController(IEmailService emailService, IEmailSettingsProvider emailSettings,
        IUploadArchive archive, ILatestUploadRepository latestUploads,
        UserManager<AppIdentityUser> userManager)
    {
        _emailService = emailService;
        _emailSettings = emailSettings;
        _archive = archive;
        _latestUploads = latestUploads;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.ToEmail = User.Identity?.Name ?? string.Empty;
        return View(await BuildReadinessAsync(cancellationToken));
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
        bool forceDownloadLink,
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
        var latest = await FindLatestAsync(kind, Scope(user), cancellationToken);

        var realArchived = !string.IsNullOrWhiteSpace(latest?.ArchivedFilePath);
        var notification = latest is null
            ? BuildIllustrativeSample(kind, withRejectedRows)
            : BuildFromRealUpload(kind, latest, forceDownloadLink);

        try
        {
            await _emailService.SendUploadCompletedAsync(
                notification, companyName, recipients, [], cancellationToken);

            TempData["StatusMessage"] = DescribeWhatWasSent(kind, recipients, notification, latest is not null,
                realArchived, forceDownloadLink);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to send email: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Says which of the two samples went out and what its links will do, because the mail itself
    /// cannot: it omits a link rather than explaining why it has none, so an absent download link
    /// looks exactly like a fault when it is simply an upload made before archiving existed.
    /// </summary>
    private static string DescribeWhatWasSent(UploadNotificationKind kind, IReadOnlyList<string> recipients,
        UploadNotificationDto notification, bool fromRealUpload, bool realArchived, bool forceDownloadLink)
    {
        var to = string.Join(", ", recipients);
        if (!fromRealUpload)
        {
            return $"Sample {kind} notification sent to {to}. It uses illustrative figures and carries " +
                   $"no links, because no completed {kind} upload was found to point at.";
        }

        var opening = $"{kind} notification sent to {to}, based on the real upload of " +
                      $"'{notification.OriginalFileName}'.";

        if (realArchived) return $"{opening} It includes a working link to download that file.";

        return forceDownloadLink
            ? $"{opening} The download link was forced in so the layout can be reviewed, but that " +
              "upload predates the archive and the workbook was never kept — following the link will " +
              "report the file as unavailable. Only an upload made after the archive is configured " +
              "carries a link that works."
            : $"{opening} It has no file-download link: that upload was made before the archive was " +
              "configured, so the workbook itself was not kept.";
    }

    /// <summary>
    /// An Administrator looks across companies, as they do everywhere else here: GMK staff sit in
    /// their own company, which has no distributor uploads, so scoping them to it would mean the
    /// sample never found anything real. A distributor stays scoped to their own company.
    /// </summary>
    private int? Scope(AppIdentityUser user) => User.IsInRole("Administrator") ? null : user.CompanyId;

    private async Task<LatestUploadSummary?> FindLatestAsync(UploadNotificationKind kind, int? scope,
        CancellationToken cancellationToken) => kind switch
    {
        UploadNotificationKind.Stock => await _latestUploads.GetLatestStockAsync(scope, cancellationToken),
        UploadNotificationKind.Sellout => await _latestUploads.GetLatestSelloutAsync(scope, cancellationToken),
        _ => null
    };

    /// <summary>
    /// Runs the archive round trip and resolves the links a notification sent right now would carry,
    /// so both requirements for a download link can be checked on this screen instead of by uploading
    /// a workbook and reading the mail that follows.
    /// </summary>
    private async Task<UploadLinkReadinessReport> BuildReadinessAsync(CancellationToken cancellationToken)
    {
        var settings = await _emailSettings.GetAsync(cancellationToken);
        var archive = await _archive.CheckAsync(cancellationToken);

        var user = await _userManager.GetUserAsync(User);
        var scope = user is null ? null : Scope(user);

        var states = new List<LatestUploadState>();
        foreach (var kind in new[] { UploadNotificationKind.Stock, UploadNotificationKind.Sellout })
        {
            var latest = await FindLatestAsync(kind, scope, cancellationToken);
            states.Add(new LatestUploadState(kind, latest,
                await IsStillOnDiskAsync(latest?.ArchivedFilePath, cancellationToken)));
        }

        // The scope is the role's reach; the viewer's company is their own. They differ for an
        // Administrator, whose sample may come from a distributor whose result page they cannot open.
        return UploadLinkReadiness.Build(settings, archive, states, user?.CompanyId);
    }

    /// <summary>
    /// A recorded path is not the same as a file: a folder can be pruned, or restored to the wrong
    /// place. The mail offers the link on the strength of the recorded path alone, so this is the
    /// only place the difference shows up before a distributor finds it.
    /// </summary>
    private async Task<bool> IsStillOnDiskAsync(string? storedPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return false;

        await using var stream = await _archive.OpenAsync(storedPath, cancellationToken);
        return stream is not null;
    }

    /// <summary>
    /// A sample against the company's real upload, so the result link and — when the workbook was
    /// archived — the download link both work when clicked.
    /// </summary>
    private static UploadNotificationDto BuildFromRealUpload(UploadNotificationKind kind,
        LatestUploadSummary latest, bool forceDownloadLink) => new()
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
        // Forcing this on is the only way to see the download line before a workbook has ever been
        // archived. It is a layout review, not a working link, and the status message says so —
        // which is why it is opt-in rather than the default.
        SourceFileArchived = forceDownloadLink || !string.IsNullOrWhiteSpace(latest.ArchivedFilePath)
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
