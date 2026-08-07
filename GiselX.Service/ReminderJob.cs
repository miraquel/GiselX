using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Interface;

namespace GiselX.Service;

public class ReminderJob
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUploadCheckService _uploadCheckService;
    private readonly IEmailService _emailService;

    public ReminderJob(
        ICompanyRepository companyRepository,
        IUploadCheckService uploadCheckService,
        IEmailService emailService)
    {
        _companyRepository = companyRepository;
        _uploadCheckService = uploadCheckService;
        _emailService = emailService;
    }

    public async Task ExecuteAsync()
    {
        var today = DateTime.Today;
        var companies = await _companyRepository.GetAllWithContactEmailAsync(CancellationToken.None);

        foreach (var company in companies)
        {
            foreach (var (deadlineDate, isFinalReminder) in ResolveTriggers(company, today))
            {
                var isWeekly = company.DeadlineDaysOfWeek.HasValue;
                var hasUploaded = await _uploadCheckService.HasUploadedThisPeriodAsync(company.Id, isWeekly);
                if (!hasUploaded)
                    await _emailService.SendReminderAsync(company.ContactEmail!, company.Name, deadlineDate, isFinalReminder);
            }
        }
    }

    private static IEnumerable<(DateTime DeadlineDate, bool IsFinalReminder)> ResolveTriggers(Company company, DateTime today)
    {
        if (company.DeadlineDayOfMonth.HasValue)
        {
            var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
            var day = Math.Min(company.DeadlineDayOfMonth.Value, daysInMonth);
            var deadline = new DateTime(today.Year, today.Month, day);

            if (today.Date == deadline.Date)
                yield return (deadline, true);
            else if (today.Date == deadline.Date.AddDays(-company.ReminderLeadDays))
                yield return (deadline, false);
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

                if (today.Date == nextOccurrence.Date)
                    yield return (nextOccurrence, true);
                else if (today.Date.AddDays(company.ReminderLeadDays) == nextOccurrence.Date)
                    yield return (nextOccurrence, false);
            }
        }
    }
}
