using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class UploadNotificationServiceTests
{
    [Fact]
    public async Task Distributor_and_internal_recipients_are_addressed_separately()
    {
        var email = new CapturingEmailService();
        var service = CreateService(email, new UploadNotificationRecipients
        {
            CompanyId = 7,
            CompanyName = "Anak Mas",
            ContactEmail = "admin@anakmas.co.id",
            BusinessUnitName = "B2B",
            InternalNotificationEmails = "scm@gandummas.co.id; b2b@gandummas.co.id"
        });

        await service.NotifyUploadCompletedAsync(Notification(), CancellationToken.None);

        Assert.Equal("Anak Mas", email.CompanyName);
        Assert.Equal(["admin@anakmas.co.id"], email.DistributorRecipients);
        Assert.Equal(["scm@gandummas.co.id", "b2b@gandummas.co.id"], email.InternalRecipients);
    }

    [Fact]
    public async Task A_company_without_a_contact_email_still_notifies_the_internal_team()
    {
        var email = new CapturingEmailService();
        var service = CreateService(email, new UploadNotificationRecipients
        {
            CompanyId = 7,
            CompanyName = "Anak Mas",
            ContactEmail = null,
            InternalNotificationEmails = "scm@gandummas.co.id"
        });

        await service.NotifyUploadCompletedAsync(Notification(), CancellationToken.None);

        Assert.Empty(email.DistributorRecipients!);
        Assert.Equal(["scm@gandummas.co.id"], email.InternalRecipients);
    }

    [Fact]
    public async Task A_business_unit_without_internal_emails_still_notifies_the_distributor()
    {
        var email = new CapturingEmailService();
        var service = CreateService(email, new UploadNotificationRecipients
        {
            CompanyId = 7,
            CompanyName = "Anak Mas",
            ContactEmail = "admin@anakmas.co.id",
            InternalNotificationEmails = null
        });

        await service.NotifyUploadCompletedAsync(Notification(), CancellationToken.None);

        Assert.Equal(["admin@anakmas.co.id"], email.DistributorRecipients);
        Assert.Empty(email.InternalRecipients!);
    }

    [Fact]
    public async Task Nothing_is_sent_when_neither_side_has_an_address()
    {
        var email = new CapturingEmailService();
        var service = CreateService(email, new UploadNotificationRecipients
        {
            CompanyId = 7, CompanyName = "Anak Mas", ContactEmail = null, InternalNotificationEmails = null
        });

        await service.NotifyUploadCompletedAsync(Notification(), CancellationToken.None);

        Assert.False(email.WasCalled);
    }

    [Fact]
    public async Task Nothing_is_sent_when_no_smtp_host_is_configured()
    {
        // Blank SmtpHost is how appsettings ships. Attempting the send would fail and be retried by
        // Hangfire ten times for every upload, so the job has to recognise it and stop.
        var email = new CapturingEmailService();
        var service = CreateService(email, new UploadNotificationRecipients
        {
            CompanyId = 7, CompanyName = "Anak Mas", ContactEmail = "admin@anakmas.co.id"
        }, smtpHost: "");

        await service.NotifyUploadCompletedAsync(Notification(), CancellationToken.None);

        Assert.False(email.WasCalled);
    }

    [Fact]
    public async Task Nothing_is_sent_when_the_company_has_since_been_removed()
    {
        var email = new CapturingEmailService();
        var service = CreateService(email, recipients: null);

        await service.NotifyUploadCompletedAsync(Notification(), CancellationToken.None);

        Assert.False(email.WasCalled);
    }

    private static UploadNotificationService CreateService(IEmailService email,
        UploadNotificationRecipients? recipients, string smtpHost = "smtp.gandummas.co.id") =>
        new(new StubRepository(recipients), email,
            new FixedEmailSettingsProvider(new EmailSettings { SmtpHost = smtpHost }),
            NullLogger<UploadNotificationService>.Instance);

    private static UploadNotificationDto Notification() => new()
    {
        CompanyId = 7,
        Kind = UploadNotificationKind.Stock,
        Reference = 42,
        TotalRows = 100,
        ValidRows = 98,
        InvalidRows = 2,
        UploadedBy = "budi",
        CompletedAtUtc = new DateTime(2026, 9, 15, 3, 0, 0, DateTimeKind.Utc)
    };

    private sealed class StubRepository(UploadNotificationRecipients? recipients) : IUploadNotificationRepository
    {
        public Task<UploadNotificationRecipients?> GetRecipientsAsync(int companyId,
            CancellationToken cancellationToken) => Task.FromResult(recipients);
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public bool WasCalled { get; private set; }
        public string? CompanyName { get; private set; }
        public IReadOnlyCollection<string>? DistributorRecipients { get; private set; }
        public IReadOnlyCollection<string>? InternalRecipients { get; private set; }

        public Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName,
            DateTime deadline, bool isFinalReminder) => Task.CompletedTask;

        public Task SendUploadCompletedAsync(UploadNotificationDto notification, string companyName,
            IReadOnlyCollection<string> distributorRecipients, IReadOnlyCollection<string> internalRecipients,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            CompanyName = companyName;
            DistributorRecipients = distributorRecipients;
            InternalRecipients = internalRecipients;
            return Task.CompletedTask;
        }
    }
}
