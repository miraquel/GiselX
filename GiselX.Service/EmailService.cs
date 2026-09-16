using GiselX.Service.Dto;
using GiselX.Service.Interface;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GiselX.Service;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName,
        DateTime deadline, bool isFinalReminder)
    {
        if (recipients.Count == 0) return;

        var daysUntil = (deadline.Date - DateTime.Today).Days;
        var subject = isFinalReminder
            ? $"[Action Required] Data upload deadline is today — {companyName}"
            : $"[Reminder] Data upload due in {daysUntil} days — {companyName}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
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
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
        await client.AuthenticateAsync(_settings.Username, _settings.Password);
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

        var messages = new List<MimeMessage>();
        if (distributorRecipients.Count > 0)
            messages.Add(BuildUploadMessage(notification, companyName, distributorRecipients, forInternalTeam: false));
        if (internalRecipients.Count > 0)
            messages.Add(BuildUploadMessage(notification, companyName, internalRecipients, forInternalTeam: true));

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        foreach (var message in messages)
        {
            await client.SendAsync(message, cancellationToken);
        }
        await client.DisconnectAsync(true, cancellationToken);
    }

    private MimeMessage BuildUploadMessage(
        UploadNotificationDto notification,
        string companyName,
        IReadOnlyCollection<string> recipients,
        bool forInternalTeam)
    {
        var rendered = forInternalTeam
            ? UploadNotificationMessage.ForInternalTeam(notification, companyName, _settings)
            : UploadNotificationMessage.ForDistributor(notification, companyName, _settings);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
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
