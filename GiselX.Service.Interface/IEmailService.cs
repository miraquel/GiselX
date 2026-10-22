using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface IEmailService
{
    /// <summary>
    /// Sends the upload-deadline reminder. Takes a list rather than one address because a company's
    /// ContactEmail holds a delimited list — callers split it with
    /// <c>NotificationRecipients.Parse</c> so that parsing lives in one place. An empty list sends
    /// nothing and opens no connection.
    /// </summary>
    /// <param name="missingItems">What is still to be uploaded for the deadline, one line each; the body
    /// lists these instead of every upload type. Null lists the default set (Sellout and Stock).</param>
    Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName, DateTime deadline,
        bool isFinalReminder, IReadOnlyList<string>? missingItems = null);

    /// <summary>
    /// Announces a completed upload to both audiences over a single SMTP connection. The two lists
    /// get separate messages rather than one with everyone in it, so the distributor never sees the
    /// internal PT. GMK addresses and each side can be worded for its reader. Either list may be
    /// empty; when both are, nothing is sent and no connection is opened.
    /// </summary>
    Task SendUploadCompletedAsync(
        UploadNotificationDto notification,
        string companyName,
        IReadOnlyCollection<string> distributorRecipients,
        IReadOnlyCollection<string> internalRecipients,
        CancellationToken cancellationToken);
}
