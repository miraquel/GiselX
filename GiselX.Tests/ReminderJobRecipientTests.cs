using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using MimeKit;

namespace GiselX.Tests;

/// <summary>
/// ContactEmail holds a ';'-delimited list. The reminder used to hand the whole string to MimeKit as
/// one address, which threw and silently killed the reminder for that company.
/// </summary>
public sealed class ReminderJobRecipientTests
{
    [Fact]
    public async Task A_multi_address_contact_email_reaches_every_address()
    {
        var email = new CapturingEmailService();
        var job = CreateJob(email, "admin@anakmas.co.id; gudang@anakmas.co.id");

        await job.ExecuteAsync();

        Assert.Equal(["admin@anakmas.co.id", "gudang@anakmas.co.id"], email.Recipients);
    }

    [Fact]
    public async Task Every_address_from_the_list_is_one_MimeKit_accepts()
    {
        // The regression this guards: "a@x; b@y" passed whole to MailboxAddress throws
        // ParseException ("Unexpected ';' token"), so the reminder never left the building.
        var email = new CapturingEmailService();
        var job = CreateJob(email, "admin@anakmas.co.id; gudang@anakmas.co.id");

        await job.ExecuteAsync();

        foreach (var recipient in email.Recipients!)
        {
            var exception = Record.Exception(() => MailboxAddress.Parse(recipient));
            Assert.Null(exception);
        }
    }

    [Fact]
    public async Task A_single_address_still_works()
    {
        var email = new CapturingEmailService();
        var job = CreateJob(email, "admin@anakmas.co.id");

        await job.ExecuteAsync();

        Assert.Equal(["admin@anakmas.co.id"], email.Recipients);
    }

    [Fact]
    public async Task A_contact_list_holding_no_usable_address_sends_nothing()
    {
        var email = new CapturingEmailService();
        var job = CreateJob(email, "  ;  ");

        await job.ExecuteAsync();

        Assert.False(email.WasCalled);
    }

    [Fact]
    public async Task A_company_that_already_uploaded_is_not_reminded()
    {
        var email = new CapturingEmailService();
        var job = CreateJob(email, "admin@anakmas.co.id", hasUploaded: true);

        await job.ExecuteAsync();

        Assert.False(email.WasCalled);
    }

    private static ReminderJob CreateJob(IEmailService email, string? contactEmail, bool hasUploaded = false)
    {
        var today = DateTime.Today;
        var company = new Company
        {
            Id = 7,
            Name = "Anak Mas",
            ContactEmail = contactEmail,
            // Deadline is today, so the final reminder fires on whatever day the suite runs.
            DeadlineDayOfMonth = today.Day,
            ReminderLeadDays = 3
        };
        return new ReminderJob(new StubCompanyRepository(company), new StubUploadCheck(hasUploaded), email);
    }

    private sealed class StubUploadCheck(bool hasUploaded) : IUploadCheckService
    {
        public Task<bool> HasUploadedThisPeriodAsync(int companyId, bool isWeekly) =>
            Task.FromResult(hasUploaded);
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public bool WasCalled { get; private set; }
        public IReadOnlyCollection<string>? Recipients { get; private set; }

        public Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName,
            DateTime deadline, bool isFinalReminder)
        {
            WasCalled = true;
            Recipients = recipients;
            return Task.CompletedTask;
        }

        public Task SendUploadCompletedAsync(UploadNotificationDto notification, string companyName,
            IReadOnlyCollection<string> distributorRecipients, IReadOnlyCollection<string> internalRecipients,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCompanyRepository(Company company) : ICompanyRepository
    {
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Company>>([company]);

        public Task<Company> CreateCompanyAsync(Company value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GiselX.Domain.Common.PagedList<Company>> GetCompaniesAsync(
            GiselX.Domain.Common.PagedListRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Company> UpdateCompanyAsync(Company value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
