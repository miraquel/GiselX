using GiselX.Service.Dto;
using GiselX.Service.Interface;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GiselX.Service;

public class EmailService(IEmailSettingsProvider settingsProvider) : IEmailService
{
    public async Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName,
        DateTime deadline, bool isFinalReminder, IReadOnlyList<string>? missingItems = null)
    {
        if (recipients.Count == 0) return;

        var settings = await settingsProvider.GetAsync(CancellationToken.None);

        // An environment nobody has configured has no SMTP host. Connecting to "" throws, and the daily
        // job would fail for every company, every day; the upload notification already returns here.
        if (string.IsNullOrWhiteSpace(settings.SmtpHost)) return;

        var subject = ReminderMessage.BuildSubject(companyName, deadline, isFinalReminder);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        foreach (var recipient in recipients)
        {
            // Parse, not the (name, address) constructor: everyone here belongs to the same company,
            // so labelling each address with the company name reads oddly in a multi-recipient To.
            message.To.Add(MailboxAddress.Parse(recipient));
        }
        message.Subject = subject;
        message.Body = new TextPart("plain")
        {
            Text = ReminderMessage.BuildBody(companyName, deadline, isFinalReminder, settings, missingItems)
        };

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

}
