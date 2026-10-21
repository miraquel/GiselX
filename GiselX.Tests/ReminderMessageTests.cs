using GiselX.Service;

namespace GiselX.Tests;

public sealed class ReminderMessageTests
{
    [Fact]
    public void An_early_reminder_names_the_deadline_in_indonesian()
    {
        var subject = ReminderMessage.BuildSubject("Anak Mas", new DateTime(2026, 9, 28), isFinalReminder: false);
        var body = ReminderMessage.BuildBody("Anak Mas", new DateTime(2026, 9, 28), false, Settings());

        Assert.Equal("[Pengingat] Batas waktu upload data 28 September 2026 - Anak Mas", subject);
        Assert.Contains("Yth. Anak Mas,", body);
        Assert.Contains("Batas Waktu       : 28 September 2026", body);
        Assert.Contains("Sellout (Sales Transaction)", body);
        Assert.Contains("Mohon tidak membalas email ini.", body);
        // No English left in the message at all.
        Assert.DoesNotContain("Dear ", body);
        Assert.DoesNotContain("reminder", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_final_reminder_says_the_deadline_is_today()
    {
        var subject = ReminderMessage.BuildSubject("Anak Mas", DateTime.Today, isFinalReminder: true);
        var body = ReminderMessage.BuildBody("Anak Mas", DateTime.Today, true, Settings());

        Assert.StartsWith("[Peringatan] Batas waktu upload data hari ini", subject);
        Assert.Contains("HARI INI", body);
        Assert.Contains("Jatuh tempo hari ini", body);
        // "Sisa Waktu" would read as "0 hari lagi" on the day itself, which says nothing.
        Assert.DoesNotContain("Sisa Waktu", body);
    }

    [Fact]
    public void The_remaining_days_are_shown_only_before_the_day_itself()
    {
        var body = ReminderMessage.BuildBody("Anak Mas", DateTime.Today.AddDays(3), false, Settings());

        Assert.Contains("Sisa Waktu        : 3 hari lagi", body);
    }

    [Fact]
    public void An_unconfigured_dashboard_and_support_omit_their_lines()
    {
        var body = ReminderMessage.BuildBody("Anak Mas", DateTime.Today.AddDays(2), false, new EmailSettings
        {
            FromName = "GiselX System"
        });

        Assert.DoesNotContain("http", body);
        Assert.DoesNotContain("hubungi tim support", body);
        Assert.Contains("Terima kasih atas kerja sama Anda.", body);
    }

    private static EmailSettings Settings() => new()
    {
        FromName = "GiselX System",
        DashboardBaseUrl = "https://giselx-dev.gandummas.co.id/",
        SupportEmail = "support@gandummas.co.id",
        SupportPhone = "021-5555-1234"
    };
}
