using System.Globalization;
using System.Text;
using GiselX.Service.Dto;

namespace GiselX.Service;

/// <summary>
/// Renders the completion mail. It is separate from <see cref="EmailService"/> so the wording can be
/// asserted without an SMTP server — the body is the part people actually read, and it was
/// previously only reachable through a live send.
///
/// Lines whose value the system does not have are omitted rather than printed empty: Stock has no
/// outlets, Service Level has no reporting period or batch to link to, and the support contact and
/// dashboard address are optional configuration.
/// </summary>
public static class UploadNotificationMessage
{
    private const int LabelWidth = 18;
    private static readonly string Rule = new('─', 46);

    /// <summary>
    /// The culture the whole mail is written in. Exposed so the upload services format the dates
    /// they bake into PeriodLabel the same way — otherwise the summary reads "Snapshot 10 August
    /// 2026" next to "Tanggal Upload : 15 September 2026".
    /// </summary>
    public static CultureInfo Culture { get; } = ResolveCulture();

    public sealed record Rendered(string Subject, string Body);

    public static Rendered ForDistributor(UploadNotificationDto notification, string companyName,
        EmailSettings settings) => Render(notification, companyName, settings, forInternalTeam: false);

    public static Rendered ForInternalTeam(UploadNotificationDto notification, string companyName,
        EmailSettings settings) => Render(notification, companyName, settings, forInternalTeam: true);

    private static Rendered Render(UploadNotificationDto notification, string companyName,
        EmailSettings settings, bool forInternalTeam)
    {
        var culture = Culture;
        var label = DescribeKind(notification.Kind);
        var partial = notification.InvalidRows > 0;
        var local = ToLocalTime(notification.CompletedAtUtc, settings, out var zoneLabel);

        var subject = BuildSubject(notification, companyName, label, partial, forInternalTeam, culture);

        var body = new StringBuilder();
        body.Append(forInternalTeam
            ? $"Halo Tim {SystemName(settings)},\r\n\r\n" +
              $"Distributor {companyName} baru saja menyelesaikan upload data {label.ToLowerInvariant()}.\r\n"
            : $"Yth. {companyName},\r\n\r\n" +
              $"Kami informasikan bahwa proses upload data {label.ToLowerInvariant()} Anda telah selesai " +
              "dan berhasil diproses oleh sistem.\r\n");

        body.Append("\r\nRINGKASAN UPLOAD\r\n");
        body.Append(Rule).Append("\r\n");
        if (forInternalTeam) AppendLine(body, "Distributor", companyName);
        AppendLine(body, "Nama File", notification.OriginalFileName);
        AppendLine(body, "Periode Data", notification.PeriodLabel);
        AppendLine(body, "Total Baris Data", notification.TotalRows.ToString("N0", culture));
        AppendLine(body, "Baris Diterima", notification.ValidRows.ToString("N0", culture));
        if (partial) AppendLine(body, "Baris Ditolak", notification.InvalidRows.ToString("N0", culture));
        AppendLine(body, "Jumlah Outlet", notification.OutletCount?.ToString("N0", culture));
        AppendLine(body, "Jumlah Produk", notification.ProductCount?.ToString("N0", culture));
        AppendLine(body, "Status", partial
            ? $"⚠️ Berhasil sebagian ({notification.InvalidRows:N0} baris ditolak)"
            : "✅ Berhasil");
        AppendLine(body, "Tanggal Upload", local.ToString("dd MMMM yyyy", culture));
        AppendLine(body, "Waktu Upload", $"{local:HH:mm} {zoneLabel}");
        AppendLine(body, "Diunggah oleh", notification.UploadedBy);
        AppendLine(body, "Referensi", notification.Reference is null ? null : $"#{notification.Reference}");
        body.Append(Rule).Append("\r\n");

        if (partial)
        {
            // The plain totals above do not say this, and it is the one thing that needs acting on:
            // the rejected rows are not in the system and will not arrive without another upload.
            body.Append($"\r\n{notification.InvalidRows:N0} baris ditolak dan TIDAK tersimpan di sistem. " +
                        "Silakan periksa detail hasil upload untuk melihat baris mana yang gagal beserta " +
                        "alasannya, perbaiki, lalu unggah ulang file tersebut.\r\n");
        }
        else if (!forInternalTeam)
        {
            body.Append($"\r\nData {label.ToLowerInvariant()} Anda kini sudah tersedia dan dapat diakses " +
                        "melalui dashboard sistem.\r\n");
        }

        var link = BuildResultLink(notification, settings);
        if (link is not null)
        {
            body.Append("\r\nUntuk melihat detail hasil upload, silakan buka tautan berikut:\r\n");
            body.Append(link).Append("\r\n");
        }

        var sourceLink = BuildSourceFileLink(notification, settings);
        if (sourceLink is not null)
        {
            body.Append("\r\nUntuk mengunduh kembali file yang Anda unggah:\r\n");
            body.Append(sourceLink).Append("\r\n");
        }

        if (!forInternalTeam)
        {
            var support = BuildSupportSentence(settings);
            if (support is not null) body.Append("\r\n").Append(support).Append("\r\n");
            body.Append("\r\nTerima kasih atas kerja sama Anda.\r\n");
        }

        body.Append($"\r\nHormat kami,\r\nTim {SystemName(settings)}\r\n");
        body.Append("\r\n---\r\nEmail ini dikirim secara otomatis oleh sistem. " +
                    "Mohon tidak membalas email ini.\r\n");

        return new Rendered(subject, body.ToString());
    }

    private static string BuildSubject(UploadNotificationDto notification, string companyName, string label,
        bool partial, bool forInternalTeam, CultureInfo culture)
    {
        var outcome = partial ? "Upload Data Berhasil Sebagian" : "Upload Data Berhasil";
        var period = string.IsNullOrWhiteSpace(notification.PeriodLabel)
            ? string.Empty
            : $" - {notification.PeriodLabel}";

        return forInternalTeam
            ? $"[{label} Report] {outcome} - {companyName}{period}"
            : $"[{label} Report] {outcome}{period}";
    }

    /// <summary>Omits the whole line when there is no value, rather than printing a blank field.</summary>
    private static void AppendLine(StringBuilder body, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        body.Append(label.PadRight(LabelWidth)).Append(": ").Append(value).Append("\r\n");
    }

    /// <summary>
    /// Public so the Email Test screen can show the very link the mail would carry. A screen that
    /// built its own copy of these URLs could report a link that works while the mail sends one that
    /// does not, which is exactly the kind of check that is worse than none.
    /// </summary>
    public static string? BuildResultLink(UploadNotificationDto notification, EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DashboardBaseUrl) || notification.Reference is null) return null;

        var root = settings.DashboardBaseUrl.TrimEnd('/');
        return notification.Kind switch
        {
            UploadNotificationKind.Sellout => $"{root}/SalesTransactions/UploadResult?groupId={notification.Reference}",
            UploadNotificationKind.Stock => $"{root}/Stocks/Result?batchId={notification.Reference}",
            _ => null
        };
    }

    /// <summary>
    /// Link to the archived workbook, offered only when one was actually kept — an upload made while
    /// archiving was switched off has nothing behind the link. The page itself requires a login and
    /// is scoped to the reader's own company, so the link in a forwarded mail grants nothing.
    ///
    /// Public for the same reason as <see cref="BuildResultLink"/>.
    /// </summary>
    public static string? BuildSourceFileLink(UploadNotificationDto notification, EmailSettings settings)
    {
        if (!notification.SourceFileArchived) return null;
        if (string.IsNullOrWhiteSpace(settings.DashboardBaseUrl) || notification.Reference is null) return null;

        var root = settings.DashboardBaseUrl.TrimEnd('/');
        return notification.Kind switch
        {
            UploadNotificationKind.Sellout =>
                $"{root}/SalesTransactions/DownloadSource?groupId={notification.Reference}",
            UploadNotificationKind.Stock =>
                $"{root}/Stocks/DownloadSource?batchId={notification.Reference}",
            _ => null
        };
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
            : "Jika Anda menemukan ketidaksesuaian data atau memerlukan bantuan lebih lanjut, " +
              $"silakan hubungi tim support kami di {contact}.";
    }

    /// <summary>
    /// Upload times are stored in UTC, but the reader is in Indonesia — "03:00" for a 10:00 upload
    /// would look like a bug. Falls back to UTC when the configured zone is unknown on the host
    /// rather than throwing inside a background job.
    /// </summary>
    private static DateTime ToLocalTime(DateTime utc, EmailSettings settings, out string zoneLabel)
    {
        var id = string.IsNullOrWhiteSpace(settings.TimeZoneId) ? "SE Asia Standard Time" : settings.TimeZoneId;
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
            zoneLabel = DescribeOffset(zone.GetUtcOffset(local));
            return local;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zoneLabel = "UTC";
            return utc;
        }
    }

    /// <summary>Indonesia names its three zones by offset, so the label follows the offset.</summary>
    private static string DescribeOffset(TimeSpan offset) => offset.Hours switch
    {
        7 when offset.Minutes == 0 => "WIB",
        8 when offset.Minutes == 0 => "WITA",
        9 when offset.Minutes == 0 => "WIT",
        _ => $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours)}"
    };

    private static string SystemName(EmailSettings settings) =>
        string.IsNullOrWhiteSpace(settings.FromName) ? "GiselX" : settings.FromName;

    private static string DescribeKind(UploadNotificationKind kind) => kind switch
    {
        UploadNotificationKind.Sellout => "Sellout",
        UploadNotificationKind.Stock => "Stock",
        UploadNotificationKind.ServiceLevel => "Service Level",
        _ => kind.ToString()
    };

    /// <summary>
    /// Indonesian month names need the id-ID culture. A host built with InvariantGlobalization has
    /// no such culture, so this degrades to invariant rather than failing the notification.
    /// </summary>
    private static CultureInfo ResolveCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("id-ID");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
