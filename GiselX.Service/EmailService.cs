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
        var kindLabel = DescribeKind(notification.Kind);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = forInternalTeam
            ? $"[Upload] {kindLabel} received from {companyName}"
            : $"[GiselX] Your {kindLabel} upload was received";
        message.Body = new TextPart("plain")
        {
            Text = BuildUploadBody(notification, companyName, kindLabel, forInternalTeam)
        };
        return message;
    }

    private static string BuildUploadBody(
        UploadNotificationDto notification,
        string companyName,
        string kindLabel,
        bool forInternalTeam)
    {
        var greeting = forInternalTeam
            ? $"{companyName} has completed a {kindLabel} upload."
            : $"Dear {companyName},\r\n\r\nWe have received your {kindLabel} upload.";

        var lines = new List<string>
        {
            $"  Distributor   : {companyName}",
            $"  Data          : {kindLabel}"
        };
        if (!string.IsNullOrWhiteSpace(notification.PeriodLabel))
            lines.Add($"  Period        : {notification.PeriodLabel}");
        if (!string.IsNullOrWhiteSpace(notification.OriginalFileName))
            lines.Add($"  File          : {notification.OriginalFileName}");
        lines.Add($"  Rows accepted : {notification.ValidRows:N0} of {notification.TotalRows:N0}");
        if (notification.InvalidRows > 0)
            lines.Add($"  Rows rejected : {notification.InvalidRows:N0}");
        lines.Add($"  Uploaded by   : {notification.UploadedBy}");
        lines.Add($"  Completed at  : {notification.CompletedAtUtc:dd MMMM yyyy HH:mm} UTC");
        if (notification.Reference is not null)
            lines.Add($"  Reference     : #{notification.Reference}");

        // A partial upload is the one case worth spelling out: the rows that did not load are gone
        // unless somebody re-uploads them, and the plain totals above do not say that on their own.
        var warning = notification.InvalidRows > 0
            ? $"\r\n{notification.InvalidRows:N0} row(s) were rejected and are NOT in the system. " +
              "Open the upload result in GiselX to see which rows failed and why, correct them, and " +
              "upload the corrected file again.\r\n"
            : string.Empty;

        var closing = forInternalTeam
            ? "This is an automated notification from GiselX."
            : "No action is required if the totals above match your file.\r\n\r\nRegards,\r\nGiselX System";

        return $"{greeting}\r\n\r\n{string.Join("\r\n", lines)}\r\n{warning}\r\n{closing}\r\n";
    }

    private static string DescribeKind(UploadNotificationKind kind) => kind switch
    {
        UploadNotificationKind.Sellout => "Sellout",
        UploadNotificationKind.Stock => "Stock",
        UploadNotificationKind.ServiceLevel => "Service Level",
        _ => kind.ToString()
    };

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
