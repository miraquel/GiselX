using GiselX.Domain;

namespace GiselX.Service;

/// <summary>
/// The uploads that count towards one deadline: everything from the day after the previous deadline up to
/// and including the deadline itself. A reminder asks about a deadline, not about the calendar week or
/// month it happens to be sent in - a Saturday lead reminder for a Monday deadline used to check the
/// previous week, and a distributor who uploaded on Saturday was still told on Monday that nothing had
/// arrived.
/// </summary>
public static class ReminderCycle
{
    /// <summary>Server-local [From, To) window for the given deadline; To is the day after it.</summary>
    public static (DateTime From, DateTime To) WindowFor(Company company, DateTime deadline)
    {
        var day = deadline.Date;
        return (PreviousDeadline(company, day).AddDays(1), day.AddDays(1));
    }

    /// <summary>The company's deadline date in the given month, the day clamped to the month's length.</summary>
    public static DateTime MonthlyDeadline(int deadlineDayOfMonth, int year, int month) =>
        new(year, month, Math.Min(deadlineDayOfMonth, DateTime.DaysInMonth(year, month)));

    private static DateTime PreviousDeadline(Company company, DateTime deadline)
    {
        if (company.DeadlineDayOfMonth is { } dayOfMonth)
        {
            var previousMonth = deadline.AddMonths(-1);
            return MonthlyDeadline(dayOfMonth, previousMonth.Year, previousMonth.Month);
        }

        if (company.DeadlineDaysOfWeek is { } days)
        {
            for (var back = 1; back <= 7; back++)
            {
                var candidate = deadline.AddDays(-back);
                if (days.HasFlag((WeekDays)(1 << (int)candidate.DayOfWeek))) return candidate;
            }
        }

        // No schedule: a one-week window, which only the email test screen ever asks for.
        return deadline.AddDays(-7);
    }
}
