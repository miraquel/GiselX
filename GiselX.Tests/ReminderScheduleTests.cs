using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Tests;

/// <summary>
/// A reminder asks about one deadline, so it must look at the uploads made for that deadline: everything
/// since the previous deadline. Checking "the current calendar week or month" instead made a Saturday lead
/// reminder for a Monday deadline look at the wrong week, and a lead reminder that fell in the previous
/// month never fired at all.
/// </summary>
public sealed class ReminderScheduleTests
{
    [Fact]
    public void A_monthly_cycle_runs_from_the_day_after_the_previous_deadline_through_the_deadline()
    {
        var company = new Company { DeadlineDayOfMonth = 5 };

        var (from, to) = ReminderCycle.WindowFor(company, new DateTime(2026, 11, 5));

        Assert.Equal(new DateTime(2026, 10, 6), from);
        Assert.Equal(new DateTime(2026, 11, 6), to);
    }

    [Fact]
    public void A_monthly_deadline_on_the_31st_is_clamped_in_short_months()
    {
        var company = new Company { DeadlineDayOfMonth = 31 };

        var (from, to) = ReminderCycle.WindowFor(company, new DateTime(2026, 11, 30));

        Assert.Equal(new DateTime(2026, 11, 1), from);
        Assert.Equal(new DateTime(2026, 12, 1), to);
    }

    [Fact]
    public void A_weekly_cycle_runs_from_the_day_after_the_previous_deadline_day()
    {
        var company = new Company { DeadlineDaysOfWeek = WeekDays.Monday };

        var (from, to) = ReminderCycle.WindowFor(company, new DateTime(2026, 11, 2)); // Monday

        Assert.Equal(new DateTime(2026, 10, 27), from); // Tuesday
        Assert.Equal(new DateTime(2026, 11, 3), to);
    }

    [Fact]
    public void Two_deadline_days_a_week_split_the_week_between_them()
    {
        var company = new Company { DeadlineDaysOfWeek = WeekDays.Monday | WeekDays.Thursday };

        var (from, to) = ReminderCycle.WindowFor(company, new DateTime(2026, 11, 5)); // Thursday

        Assert.Equal(new DateTime(2026, 11, 3), from); // Tuesday, after Monday's deadline
        Assert.Equal(new DateTime(2026, 11, 6), to);
    }

    [Fact]
    public async Task A_lead_reminder_that_falls_in_the_previous_month_is_sent()
    {
        // Deadline on the 3rd with five days' lead: the reminder belongs on 29 October.
        var email = new RecordingEmail();
        var job = Job(email, new DateTime(2026, 10, 29),
            new Company { Id = 1, Name = "A", ContactEmail = "a@x.co", DeadlineDayOfMonth = 3, ReminderLeadDays = 5 });

        await job.ExecuteAsync();

        var sent = Assert.Single(email.Sent);
        Assert.Equal(new DateTime(2026, 11, 3), sent.Deadline);
        Assert.False(sent.IsFinal);
    }

    [Fact]
    public async Task The_final_reminder_is_sent_on_the_deadline()
    {
        var email = new RecordingEmail();
        var job = Job(email, new DateTime(2026, 11, 3),
            new Company { Id = 1, Name = "A", ContactEmail = "a@x.co", DeadlineDayOfMonth = 3, ReminderLeadDays = 5 });

        await job.ExecuteAsync();

        Assert.True(Assert.Single(email.Sent).IsFinal);
    }

    [Fact]
    public async Task The_upload_check_covers_the_deadlines_cycle_not_the_calendar_week_of_the_reminder()
    {
        // Saturday lead reminder for a Monday deadline: the check must cover Tuesday through Monday.
        var check = new RecordingCheck(["Stock"]);
        var job = new ReminderJob(new Companies(new Company
            { Id = 1, Name = "A", ContactEmail = "a@x.co", DeadlineDaysOfWeek = WeekDays.Monday, ReminderLeadDays = 2 }),
            check, new RecordingEmail(), new FixedClock(new DateTime(2026, 10, 31)));

        await job.ExecuteAsync();

        Assert.Equal((new DateTime(2026, 10, 27), new DateTime(2026, 11, 3)), Assert.Single(check.Windows));
    }

    [Fact]
    public async Task The_reminder_names_what_is_missing()
    {
        var email = new RecordingEmail();
        var job = new ReminderJob(new Companies(new Company
            { Id = 1, Name = "A", ContactEmail = "a@x.co", DeadlineDayOfMonth = 3 }),
            new RecordingCheck(["Stock area Binjai"]), email, new FixedClock(new DateTime(2026, 11, 3)));

        await job.ExecuteAsync();

        Assert.Equal(["Stock area Binjai"], Assert.Single(email.Sent).Missing);
    }

    [Fact]
    public async Task A_company_with_nothing_missing_is_not_reminded()
    {
        var email = new RecordingEmail();
        var job = new ReminderJob(new Companies(new Company
            { Id = 1, Name = "A", ContactEmail = "a@x.co", DeadlineDayOfMonth = 3 }),
            new RecordingCheck([]), email, new FixedClock(new DateTime(2026, 11, 3)));

        await job.ExecuteAsync();

        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task One_company_failing_does_not_stop_the_reminders_of_the_others()
    {
        var email = new RecordingEmail { FailFor = "Broken" };
        var job = Job(email, new DateTime(2026, 11, 3),
            new Company { Id = 1, Name = "Broken", ContactEmail = "a@x.co", DeadlineDayOfMonth = 3 },
            new Company { Id = 2, Name = "Fine", ContactEmail = "b@x.co", DeadlineDayOfMonth = 3 });

        await job.ExecuteAsync();

        Assert.Equal(["Fine"], email.Sent.Select(sent => sent.Company));
    }

    [Fact]
    public async Task Upload_check_names_sellout_stock_and_each_stock_area_without_data()
    {
        var repository = new PresenceRepository(new UploadPresence(false, true, ["Binjai"]));

        var missing = await new UploadCheckService(repository).FindMissingUploadsAsync(1,
            new DateTime(2026, 10, 6), new DateTime(2026, 11, 6), CancellationToken.None);

        Assert.Equal(["Sellout (Sales Transaction)", "Stock area Binjai"], missing);
        Assert.Equal(new DateTime(2026, 10, 6).ToUniversalTime(), repository.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 6).ToUniversalTime(), repository.ToUtc);
    }

    [Fact]
    public async Task Upload_check_reports_stock_once_when_a_company_without_areas_has_none()
    {
        var repository = new PresenceRepository(new UploadPresence(true, false, []));

        var missing = await new UploadCheckService(repository).FindMissingUploadsAsync(1,
            new DateTime(2026, 10, 6), new DateTime(2026, 11, 6), CancellationToken.None);

        Assert.Equal(["Stock"], missing);
    }

    [Fact]
    public void The_reminder_body_lists_only_the_missing_items()
    {
        var body = ReminderMessage.BuildBody("A", new DateTime(2026, 11, 3), isFinalReminder: true,
            new EmailSettings(), ["Stock area Binjai"]);

        Assert.Contains("  - Stock area Binjai", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Service Level", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reminder_without_an_smtp_host_is_not_attempted()
    {
        var service = new EmailService(new FixedEmailSettingsProvider(new EmailSettings { SmtpHost = "" }));

        var exception = await Record.ExceptionAsync(() => service.SendReminderAsync(
            ["a@x.co"], "A", new DateTime(2026, 11, 3), isFinalReminder: true));

        Assert.Null(exception);
    }

    private static ReminderJob Job(RecordingEmail email, DateTime today, params Company[] companies) =>
        new(new Companies(companies), new RecordingCheck(["Stock"]), email, new FixedClock(today));

    private sealed class FixedClock(DateTime localToday) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(localToday.AddHours(8), DateTimeKind.Utc));
    }

    private sealed class RecordingCheck(IReadOnlyList<string> missing) : IUploadCheckService
    {
        public List<(DateTime From, DateTime To)> Windows { get; } = [];

        public Task<IReadOnlyList<string>> FindMissingUploadsAsync(int companyId, DateTime fromLocal, DateTime toLocal,
            CancellationToken cancellationToken)
        {
            Windows.Add((fromLocal, toLocal));
            return Task.FromResult(missing);
        }
    }

    private sealed class RecordingEmail : IEmailService
    {
        public string? FailFor { get; init; }
        public List<(string Company, DateTime Deadline, bool IsFinal, IReadOnlyList<string>? Missing)> Sent { get; } = [];

        public Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName, DateTime deadline,
            bool isFinalReminder, IReadOnlyList<string>? missingItems = null)
        {
            if (companyName == FailFor) throw new InvalidOperationException("SMTP refused the recipient.");
            Sent.Add((companyName, deadline, isFinalReminder, missingItems));
            return Task.CompletedTask;
        }

        public Task SendUploadCompletedAsync(UploadNotificationDto notification, string companyName,
            IReadOnlyCollection<string> distributorRecipients, IReadOnlyCollection<string> internalRecipients,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PresenceRepository(UploadPresence presence) : IUploadCheckRepository
    {
        public DateTime FromUtc { get; private set; }
        public DateTime ToUtc { get; private set; }

        public Task<UploadPresence> GetUploadPresenceAsync(int companyId, DateTime fromUtc, DateTime toUtc,
            CancellationToken cancellationToken)
        {
            (FromUtc, ToUtc) = (fromUtc, toUtc);
            return Task.FromResult(presence);
        }
    }

    private sealed class Companies(params Company[] companies) : ICompanyRepository
    {
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Company>>(companies);
        public Task<Company> CreateCompanyAsync(Company value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Company> UpdateCompanyAsync(Company value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
