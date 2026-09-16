using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GiselX.Service;

public sealed class EmailSettingService(
    IEmailSettingRepository repository,
    IOptions<EmailSettings> fallback,
    IEmailSecretProtector protector) : IEmailSettingService
{
    public async Task<ServiceResponse<EmailSettingDto>> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await repository.GetAsync(cancellationToken);
        if (stored is null)
        {
            // Nothing saved yet: show what the application is actually using so the first save is an
            // edit of the truth rather than of an empty form.
            var configured = fallback.Value;
            return new ServiceResponse<EmailSettingDto>
            {
                Data = new EmailSettingDto
                {
                    SmtpHost = configured.SmtpHost,
                    SmtpPort = configured.SmtpPort,
                    EnableSsl = configured.EnableSsl,
                    Username = configured.Username,
                    FromAddress = configured.FromAddress,
                    FromName = configured.FromName,
                    DashboardBaseUrl = configured.DashboardBaseUrl,
                    SupportEmail = configured.SupportEmail,
                    SupportPhone = configured.SupportPhone,
                    TimeZoneId = configured.TimeZoneId,
                    HasStoredPassword = !string.IsNullOrEmpty(configured.Password),
                    IsFallbackFromConfiguration = true
                },
                Message = "No settings have been saved yet; showing the values from appsettings.json."
            };
        }

        return new ServiceResponse<EmailSettingDto>
        {
            Data = new EmailSettingDto
            {
                SmtpHost = stored.SmtpHost,
                SmtpPort = stored.SmtpPort,
                EnableSsl = stored.EnableSsl,
                Username = stored.Username,
                FromAddress = stored.FromAddress,
                FromName = stored.FromName,
                DashboardBaseUrl = stored.DashboardBaseUrl,
                SupportEmail = stored.SupportEmail,
                SupportPhone = stored.SupportPhone,
                TimeZoneId = stored.TimeZoneId,
                // The password itself is never sent to the screen, only whether one exists.
                HasStoredPassword = !string.IsNullOrEmpty(stored.PasswordProtected),
                ModifiedDate = stored.ModifiedDate,
                ModifiedBy = stored.ModifiedBy
            },
            Message = "Email settings retrieved successfully."
        };
    }

    public async Task<ServiceResponse> SaveAsync(EmailSettingDto dto, string username,
        CancellationToken cancellationToken)
    {
        var errors = Validate(dto);
        if (errors.Count > 0)
            return new ServiceResponse { Errors = errors, StatusCode = StatusCodes.Status400BadRequest };

        var existing = await repository.GetAsync(cancellationToken);

        await repository.SaveAsync(new EmailSetting
        {
            SmtpHost = dto.SmtpHost.Trim(),
            SmtpPort = dto.SmtpPort,
            EnableSsl = dto.EnableSsl,
            Username = Blank(dto.Username),
            PasswordProtected = ResolvePassword(dto, existing),
            FromAddress = dto.FromAddress.Trim(),
            FromName = dto.FromName.Trim(),
            DashboardBaseUrl = Blank(dto.DashboardBaseUrl)?.TrimEnd('/'),
            SupportEmail = Blank(dto.SupportEmail),
            SupportPhone = Blank(dto.SupportPhone),
            TimeZoneId = dto.TimeZoneId.Trim(),
            ModifiedDate = DateTime.UtcNow,
            ModifiedBy = username
        }, cancellationToken);

        return new ServiceResponse { Message = "Email settings saved." };
    }

    /// <summary>
    /// A blank password field means "leave what is stored alone", not "clear it" — otherwise editing
    /// the support phone number would silently break authentication. Clearing is explicit.
    /// </summary>
    private string? ResolvePassword(EmailSettingDto dto, EmailSetting? existing)
    {
        if (dto.ClearPassword) return null;
        if (!string.IsNullOrEmpty(dto.Password)) return protector.Protect(dto.Password);
        return existing?.PasswordProtected;
    }

    private static List<string> Validate(EmailSettingDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.SmtpHost)) errors.Add("SMTP Host is required.");
        if (dto.SmtpPort is < 1 or > 65535) errors.Add("SMTP Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(dto.FromAddress)) errors.Add("From Address is required.");
        else if (NotAnAddress(dto.FromAddress)) errors.Add($"'{dto.FromAddress}' is not a valid email address.");
        if (string.IsNullOrWhiteSpace(dto.FromName)) errors.Add("From Name is required.");

        if (!string.IsNullOrWhiteSpace(dto.SupportEmail) && NotAnAddress(dto.SupportEmail))
            errors.Add($"'{dto.SupportEmail}' is not a valid email address.");

        if (!string.IsNullOrWhiteSpace(dto.DashboardBaseUrl) &&
            !Uri.TryCreate(dto.DashboardBaseUrl, UriKind.Absolute, out var url))
            errors.Add("Dashboard Base URL must be a full address, for example https://giselx.example.co.id.");
        else if (!string.IsNullOrWhiteSpace(dto.DashboardBaseUrl) &&
                 Uri.TryCreate(dto.DashboardBaseUrl, UriKind.Absolute, out var parsed) &&
                 parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            errors.Add("Dashboard Base URL must use http or https.");

        if (string.IsNullOrWhiteSpace(dto.TimeZoneId)) errors.Add("Time Zone is required.");
        else if (!TimeZoneExists(dto.TimeZoneId))
            errors.Add($"'{dto.TimeZoneId}' is not a time zone this server knows. " +
                       "Use for example 'SE Asia Standard Time' or 'Asia/Jakarta'.");

        return errors;
    }

    /// <summary>Checked here rather than at send time, where an unknown zone silently becomes UTC.</summary>
    private static bool TimeZoneExists(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool NotAnAddress(string value) => NotificationRecipients.FindInvalid(value).Count > 0
                                                      || NotificationRecipients.Parse(value).Count != 1;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
