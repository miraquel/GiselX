namespace GiselX.Service.Interface;

public interface IEmailService
{
    /// <summary>
    /// Sends the upload-deadline reminder. Takes a list rather than one address because a company's
    /// ContactEmail holds a delimited list — callers split it with
    /// <c>NotificationRecipients.Parse</c> so that parsing lives in one place. An empty list sends
    /// nothing and opens no connection.
    /// </summary>
    Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName, DateTime deadline,
        bool isFinalReminder);
}
