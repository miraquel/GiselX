using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GiselX.Service;

public class ReminderJob
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUploadCheckService _uploadCheckService;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(
        ICompanyRepository companyRepository,
        IUploadCheckService uploadCheckService,
        IEmailService emailService,
        TimeProvider? timeProvider = null,
        ILogger<ReminderJob>? logger = null)
    {
        _companyRepository = companyRepository;
        _uploadCheckService = uploadCheckService;
        _emailService = emailService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<ReminderJob>.Instance;
    }

    public async Task ExecuteAsync()
    {
        var today = _timeProvider.GetLocalNow().Date;
        var companies = await _companyRepository.GetAllWithContactEmailAsync(CancellationToken.None);

        foreach (var company in companies)
        {
            // One company's failure - a refused recipient, a dropped connection - must not cost every
            // company after it its reminder. Letting it escape also made Hangfire retry the whole job,
            // which re-sent the reminders that had already gone out.
            try
            {
                await RemindAsync(company, today);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Reminder for company {CompanyId} ({CompanyName}) failed.",
                    company.Id, company.Name);
            }
        }
    }

    private async Task RemindAsync(Company company, DateTime today)
    {
        // ContactEmail is a delimited list, so it is split here rather than handed to the mailer
        // whole — passing "a@x; b@y" as one address made MimeKit throw and killed the reminder.
        var recipients = NotificationRecipients.Parse(company.ContactEmail);
        if (recipients.Count == 0) return;

        foreach (var (deadlineDate, isFinalReminder) in ResolveTriggers(company, today))
        {
            var (from, to) = ReminderCycle.WindowFor(company, deadlineDate);
            var missing = await _uploadCheckService.FindMissingUploadsAsync(company.Id, from, to,
                CancellationToken.None);
            if (missing.Count > 0)
                await _emailService.SendReminderAsync(recipients, company.Name, deadlineDate, isFinalReminder,
                    missing);
        }
    }

    private static IEnumerable<(DateTime DeadlineDate, bool IsFinalReminder)> ResolveTriggers(Company company, DateTime today)
    {
        if (company.DeadlineDayOfMonth is { } dayOfMonth)
        {
            // This month's deadline and next month's: a lead reminder for an early-month deadline falls
            // in the month before, and looking at this month alone never sent it.
            var nextMonth = today.AddMonths(1);
            foreach (var deadline in new[]
                     {
                         ReminderCycle.MonthlyDeadline(dayOfMonth, today.Year, today.Month),
                         ReminderCycle.MonthlyDeadline(dayOfMonth, nextMonth.Year, nextMonth.Month)
                     })
            {
                if (today == deadline)
                    yield return (deadline, true);
                else if (company.ReminderLeadDays > 0 && today == deadline.AddDays(-company.ReminderLeadDays))
                    yield return (deadline, false);
            }
        }
        else if (company.DeadlineDaysOfWeek.HasValue)
        {
            foreach (DayOfWeek dow in Enum.GetValues<DayOfWeek>())
            {
                var flag = (WeekDays)(1 << (int)dow);
                if (!company.DeadlineDaysOfWeek.Value.HasFlag(flag)) continue;

                // Find next occurrence of this day of week from today (0 = today if today matches)
                var daysUntil = ((int)dow - (int)today.DayOfWeek + 7) % 7;
                var nextOccurrence = today.AddDays(daysUntil);

                if (today == nextOccurrence)
                    yield return (nextOccurrence, true);
                else if (company.ReminderLeadDays > 0 && today.AddDays(company.ReminderLeadDays) == nextOccurrence)
                    yield return (nextOccurrence, false);
            }
        }
    }
}
