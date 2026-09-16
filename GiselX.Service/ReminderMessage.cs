using System.Globalization;
using System.Text;

namespace GiselX.Service;

/// <summary>
/// Renders the upload-deadline reminder. Its readers are distributors in Indonesia — the same people
/// who receive the upload confirmation — so it is written in Indonesian and laid out the same way,
/// rather than being the one English message left in the system.
///
/// Separate from <see cref="EmailService"/> so the wording can be asserted without an SMTP server.
/// </summary>
public static class ReminderMessage
{
    private const int LabelWidth = 18;
    private static readonly string Rule = new('─', 46);

    public static string BuildSubject(string companyName, DateTime deadline, bool isFinalReminder)
    {
        var culture = UploadNotificationMessage.Culture;
        return isFinalReminder
            ? $"[Peringatan] Batas waktu upload data hari ini - {companyName}"
            : $"[Pengingat] Batas waktu upload data {deadline.ToString("dd MMMM yyyy", culture)} - {companyName}";
    }

    public static string BuildBody(string companyName, DateTime deadline, bool isFinalReminder,
        EmailSettings settings)
    {
        var culture = UploadNotificationMessage.Culture;
        var daysUntil = (deadline.Date - DateTime.Today).Days;

        var body = new StringBuilder();
        body.Append($"Yth. {companyName},\r\n\r\n");
        body.Append(isFinalReminder
            ? "Kami ingatkan bahwa batas waktu pengiriman data Anda adalah HARI INI. " +
              "Sampai email ini dikirim, data Anda untuk periode berjalan belum kami terima.\r\n"
            : $"Kami ingatkan bahwa batas waktu pengiriman data Anda akan jatuh pada " +
              $"{deadline.ToString("dd MMMM yyyy", culture)}. Sampai email ini dikirim, data Anda " +
              "untuk periode berjalan belum kami terima.\r\n");

        body.Append("\r\nRINCIAN\r\n");
        body.Append(Rule).Append("\r\n");
        AppendLine(body, "Distributor", companyName);
        AppendLine(body, "Batas Waktu", deadline.ToString("dd MMMM yyyy", culture));
        // Only meaningful before the day itself; on the deadline the status line already says "today".
        if (!isFinalReminder && daysUntil > 0)
            AppendLine(body, "Sisa Waktu", $"{daysUntil} hari lagi");
        AppendLine(body, "Status", isFinalReminder
            ? "⚠️ Jatuh tempo hari ini"
            : "⏰ Belum diterima");
        body.Append(Rule).Append("\r\n");

        body.Append("\r\nData yang perlu diunggah:\r\n");
        body.Append("  - Sellout (Sales Transaction)\r\n");
        body.Append("  - Stock\r\n");
        body.Append("  - Service Level\r\n");

        if (!string.IsNullOrWhiteSpace(settings.DashboardBaseUrl))
        {
            body.Append("\r\nSilakan unggah melalui sistem GiselX:\r\n");
            body.Append(settings.DashboardBaseUrl.TrimEnd('/')).Append("\r\n");
        }

        // The reminder is sent from a scheduled job that checks the period before mailing, but the
        // check and the reader's own upload can cross; saying so avoids a needless support call.
        body.Append("\r\nJika Anda sudah melakukan upload hari ini, mohon abaikan email ini.\r\n");

        var support = BuildSupportSentence(settings);
        if (support is not null) body.Append("\r\n").Append(support).Append("\r\n");

        body.Append("\r\nTerima kasih atas kerja sama Anda.\r\n");
        body.Append($"\r\nHormat kami,\r\nTim {SystemName(settings)}\r\n");
        body.Append("\r\n---\r\nEmail ini dikirim secara otomatis oleh sistem. " +
                    "Mohon tidak membalas email ini.\r\n");

        return body.ToString();
    }

    private static void AppendLine(StringBuilder body, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        body.Append(label.PadRight(LabelWidth)).Append(": ").Append(value).Append("\r\n");
    }

    private static string? BuildSupportSentence(EmailSettings settings)
    {
        var email = settings.SupportEmail?.Trim();
        var phone = settings.SupportPhone?.Trim();
        var contact = (string.IsNullOrWhiteSpace(email), string.IsNullOrWhiteSpace(phone)) switch
        {
            (false, false) => $"{email} atau {phone}",
            (false, true) => email,
            (true, false) => phone,
            _ => null
        };

        return contact is null
            ? null
            : $"Jika Anda memerlukan bantuan, silakan hubungi tim support kami di {contact}.";
    }

    private static string SystemName(EmailSettings settings) =>
        string.IsNullOrWhiteSpace(settings.FromName) ? "GiselX" : settings.FromName;
}
