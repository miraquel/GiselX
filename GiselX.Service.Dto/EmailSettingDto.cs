using System.ComponentModel.DataAnnotations;

namespace GiselX.Service.Dto;

public sealed class EmailSettingDto
{
    [Required, StringLength(255)]
    [Display(Name = "SMTP Host")]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    [Display(Name = "SMTP Port")]
    public int SmtpPort { get; set; } = 587;

    [Display(Name = "Use STARTTLS")]
    public bool EnableSsl { get; set; } = true;

    [StringLength(255)]
    [Display(Name = "Username")]
    public string? Username { get; set; }

    /// <summary>
    /// Left blank on the form when a password is already stored — the stored one is kept rather than
    /// being cleared, so editing an unrelated field does not silently wipe the credential. Only a
    /// value typed here replaces it.
    /// </summary>
    [StringLength(255)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Clear the stored password")]
    public bool ClearPassword { get; set; }

    public bool HasStoredPassword { get; set; }

    [Required, StringLength(255)]
    [Display(Name = "From Address")]
    public string FromAddress { get; set; } = string.Empty;

    [Required, StringLength(255)]
    [Display(Name = "From Name")]
    public string FromName { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Dashboard Base URL")]
    public string? DashboardBaseUrl { get; set; }

    [StringLength(255)]
    [Display(Name = "Support Email")]
    public string? SupportEmail { get; set; }

    [StringLength(100)]
    [Display(Name = "Support Phone")]
    public string? SupportPhone { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Time Zone")]
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";

    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>True when nothing has been saved yet and the values shown come from appsettings.</summary>
    public bool IsFallbackFromConfiguration { get; set; }
}
