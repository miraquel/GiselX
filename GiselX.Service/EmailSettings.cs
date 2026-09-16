namespace GiselX.Service;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Root address of the application, e.g. https://giselx-dev.gandummas.co.id. Used to link an
    /// upload notification straight to its result page. Blank omits the link rather than sending a
    /// broken one, so an unconfigured environment still mails a usable summary.
    /// </summary>
    public string? DashboardBaseUrl { get; set; }

    /// <summary>Support contact quoted to distributors. Either may be blank; both blank omits the line.</summary>
    public string? SupportEmail { get; set; }

    public string? SupportPhone { get; set; }

    /// <summary>
    /// Zone the upload timestamps are shown in. Times are stored in UTC, and an Indonesian reader
    /// seeing 03:00 for a 10:00 upload would read it as a fault. Windows and IANA ids both work.
    /// </summary>
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
}
