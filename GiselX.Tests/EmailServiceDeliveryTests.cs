using GiselX.Service;
using GiselX.Service.Dto;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

/// <summary>
/// Exercises EmailService against a real SMTP conversation on loopback. Everything else about the
/// mail was tested at the interface, which proved the wording but never that a message could leave
/// the process.
/// </summary>
public sealed class EmailServiceDeliveryTests
{
    [Fact]
    public async Task A_reminder_is_delivered_to_every_address_on_the_list()
    {
        using var sink = new SmtpSink();
        var service = new EmailService(new FixedEmailSettingsProvider(Settings(sink.Port)));

        await service.SendReminderAsync(
            ["admin@anakmas.co.id", "gudang@anakmas.co.id"], "Anak Mas",
            new DateTime(2026, 9, 28), isFinalReminder: false);

        Assert.Equal(["admin@anakmas.co.id", "gudang@anakmas.co.id"], sink.Recipients);
        var message = Assert.Single(sink.Messages);
        Assert.Contains("Anak Mas", message);
        Assert.Contains("Subject:", message);
    }

    [Fact]
    public async Task An_upload_notification_delivers_one_message_per_audience()
    {
        using var sink = new SmtpSink();
        var service = new EmailService(new FixedEmailSettingsProvider(Settings(sink.Port)));

        await service.SendUploadCompletedAsync(Notification(), "Anak Mas",
            distributorRecipients: ["admin@anakmas.co.id"],
            internalRecipients: ["scm@gandummas.co.id", "b2b@gandummas.co.id"],
            CancellationToken.None);

        // Two messages over one connection, and neither audience is on the other's envelope.
        Assert.Equal(2, sink.Messages.Count);
        Assert.Equal(["admin@anakmas.co.id", "scm@gandummas.co.id", "b2b@gandummas.co.id"], sink.Recipients);
        Assert.DoesNotContain("gandummas.co.id", sink.Messages[0]);
        Assert.DoesNotContain("anakmas.co.id", sink.Messages[1]);
    }

    [Fact]
    public async Task The_delivered_body_carries_the_indonesian_summary_intact()
    {
        using var sink = new SmtpSink();
        var service = new EmailService(new FixedEmailSettingsProvider(Settings(sink.Port)));

        await service.SendUploadCompletedAsync(Notification(), "Anak Mas",
            ["admin@anakmas.co.id"], [], CancellationToken.None);

        // Decoded from whatever transfer encoding MimeKit chose, so this proves the accented and
        // box-drawing characters survive the wire rather than arriving as mojibake.
        var delivered = DecodeBody(Assert.Single(sink.Messages));
        Assert.Contains("RINGKASAN UPLOAD", delivered);
        Assert.Contains("Jumlah Produk", delivered);
        Assert.Contains("✅ Berhasil", delivered);
        Assert.Contains("───", delivered);
        Assert.Contains("Mohon tidak membalas email ini.", delivered);
    }

    [Fact]
    public async Task A_relay_that_requires_no_authentication_still_accepts_the_mail()
    {
        // Internal relays commonly accept mail from the application server with no credentials at
        // all. Username is then left blank, and the send must not insist on authenticating.
        using var sink = new SmtpSink(advertiseAuth: false);
        var settings = Settings(sink.Port);
        settings.Username = "";
        settings.Password = "";
        var service = new EmailService(new FixedEmailSettingsProvider(settings));

        await service.SendReminderAsync(["admin@anakmas.co.id"], "Anak Mas",
            new DateTime(2026, 9, 28), isFinalReminder: true);

        Assert.Single(sink.Messages);
        Assert.False(sink.Authenticated);
    }

    [Fact]
    public async Task Credentials_are_used_when_they_are_configured()
    {
        using var sink = new SmtpSink();
        var service = new EmailService(new FixedEmailSettingsProvider(Settings(sink.Port)));

        await service.SendReminderAsync(["admin@anakmas.co.id"], "Anak Mas",
            new DateTime(2026, 9, 28), isFinalReminder: true);

        Assert.True(sink.Authenticated);
    }

    private static string DecodeBody(string raw)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw));
        var message = MimeKit.MimeMessage.Load(stream);
        return message.TextBody ?? string.Empty;
    }

    private static EmailSettings Settings(int port) => new()
    {
        SmtpHost = "127.0.0.1",
        SmtpPort = port,
        EnableSsl = false,
        Username = "giselx",
        Password = "secret",
        FromAddress = "noreply@giselx.local",
        FromName = "GiselX System",
        DashboardBaseUrl = "https://giselx-dev.gandummas.co.id",
        TimeZoneId = "SE Asia Standard Time"
    };

    private static UploadNotificationDto Notification() => new()
    {
        CompanyId = 1,
        Kind = UploadNotificationKind.Stock,
        Reference = 901,
        TotalRows = 312,
        ValidRows = 312,
        UploadedBy = "sari.anakmas",
        CompletedAtUtc = new DateTime(2026, 9, 16, 2, 15, 0, DateTimeKind.Utc),
        OriginalFileName = "stock-16-sep.xlsx",
        PeriodLabel = "Snapshot 16 September 2026",
        ProductCount = 312
    };
}
