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

    public async Task SendReminderAsync(string toEmail, string companyName, DateTime deadline, bool isFinalReminder)
    {
        var daysUntil = (deadline.Date - DateTime.Today).Days;
        var subject = isFinalReminder
            ? $"[Action Required] Data upload deadline is today — {companyName}"
            : $"[Reminder] Data upload due in {daysUntil} days — {companyName}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(companyName, toEmail));
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
