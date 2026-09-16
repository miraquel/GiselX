namespace GiselX.Domain;

/// <summary>
/// SMTP configuration held in the database rather than in appsettings.json, because a copy-paste
/// deployment overwrites the file and takes the mail configuration with it — which is how the dev
/// server lost its connection string and failed to start. A single row; Id is always 1.
/// </summary>
public sealed class EmailSetting
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }

    /// <summary>
    /// Protected with ASP.NET Core Data Protection, never the plain password. Null when the relay
    /// needs no credentials, which is a valid configuration rather than a missing one.
    /// </summary>
    public string? PasswordProtected { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string? DashboardBaseUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";

    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
