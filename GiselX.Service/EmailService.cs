using GiselX.Service.Dto;
using GiselX.Service.Interface;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GiselX.Service;

public class EmailService(IEmailSettingsProvider settingsProvider) : IEmailService
{
    public async Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName,
        DateTime deadline, bool isFinalReminder)
    {
        if (recipients.Count == 0) return;

        var settings = await settingsProvider.GetAsync(CancellationToken.None);

        var daysUntil = (deadline.Date - DateTime.Today).Days;
        var subject = isFinalReminder
            ? $"[Action Required] Data upload deadline is today — {companyName}"
            : $"[Reminder] Data upload due in {daysUntil} days — {companyName}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        foreach (var recipient in recipients)
        {
            // Parse, not the (name, address) constructor: everyone here belongs to the same company,
            // so labelling each address with the company name reads oddly in a multi-recipient To.
            message.To.Add(MailboxAddress.Parse(recipient));
        }
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = BuildBody(companyName, deadline, isFinalReminder) };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
        await AuthenticateIfConfiguredAsync(client, settings, CancellationToken.None);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendUploadCompletedAsync(
        UploadNotificationDto notification,
        string companyName,
        IReadOnlyCollection<string> distributorRecipients,
        IReadOnlyCollection<string> internalRecipients,
        CancellationToken cancellationToken)
    {
        if (distributorRecipients.Count == 0 && internalRecipients.Count == 0) return;

        var settings = await settingsProvider.GetAsync(cancellationToken);

        var messages = new List<MimeMessage>();
        if (distributorRecipients.Count > 0)
            messages.Add(BuildUploadMessage(notification, companyName, distributorRecipients, false, settings));
        if (internalRecipients.Count > 0)
            messages.Add(BuildUploadMessage(notification, companyName, internalRecipients, true, settings));

        using var client = new SmtpClient();
        await client.ConnectAsync(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);
        await AuthenticateIfConfiguredAsync(client, settings, cancellationToken);
        foreach (var message in messages)
        {
            await client.SendAsync(message, cancellationToken);
        }
        await client.DisconnectAsync(true, cancellationToken);
    }

    /// <summary>
    /// Authenticates only when a username is configured. Internal relays commonly accept mail from
    /// the application server with no credentials and advertise no AUTH at all; asking to
    /// authenticate there fails the send outright with "The SMTP server does not support
    /// authentication", which is a configuration that should simply work.
    /// </summary>
    private static async Task AuthenticateIfConfiguredAsync(SmtpClient client, EmailSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Username)) return;

        await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
    }

    private static MimeMessage BuildUploadMessage(
        UploadNotificationDto notification,
        string companyName,
        IReadOnlyCollection<string> recipients,
        bool forInternalTeam,
        EmailSettings settings)
    {
        var rendered = forInternalTeam
            ? UploadNotificationMessage.ForInternalTeam(notification, companyName, settings)
            : UploadNotificationMessage.ForDistributor(notification, companyName, settings);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = rendered.Subject;
        message.Body = new TextPart("plain") { Text = rendered.Body };
        return message;
    }

    private static string BuildBody(string companyName, DateTime deadline, bool isFinalReminder)
    {
        var intro = isFinalReminder
            ? "This is a final reminder that your data upload deadline is TODAY."
            : $"This is a reminder that your data upload deadline is on {deadline:dd MMMM yyyy}.";

        return $"""
            Dear {companyName},

            {intro}

            Please ensure the following data has been uploaded before the deadline:
              - Sales Transactions
              - Stock Data
              - Service Level Data (TransDist)

            If you have already completed your upload, please disregard this message.

            Regards,
            GiselX System
            """;
    }
}
