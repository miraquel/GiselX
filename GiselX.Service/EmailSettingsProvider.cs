using GiselX.Repository.Interface;
using GiselX.Service.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GiselX.Service;

/// <summary>
/// Where the mailer gets its SMTP configuration. Stored settings win over appsettings.json so a
/// copy-paste deployment cannot silently revert them — the file is overwritten on every deploy, the
/// table is not. appsettings remains the fallback for an environment nobody has configured yet.
/// </summary>
public interface IEmailSettingsProvider
{
    Task<EmailSettings> GetAsync(CancellationToken cancellationToken);
}

public sealed class EmailSettingsProvider(
    IEmailSettingRepository repository,
    IOptions<EmailSettings> fallback,
    IEmailSecretProtector protector,
    ILogger<EmailSettingsProvider> logger) : IEmailSettingsProvider
{
    public async Task<EmailSettings> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await repository.GetAsync(cancellationToken);
        if (stored is null) return fallback.Value;

        var password = protector.Unprotect(stored.PasswordProtected);
        if (stored.PasswordProtected is not null && password is null)
        {
            // Worth saying out loud: the row is there and looks configured, but the password cannot
            // be read, so every send will fail authentication until somebody re-enters it.
            logger.LogError(
                "The stored SMTP password could not be decrypted. It must be entered again on the " +
                "Email Settings screen — the data-protection keys that encrypted it are gone.");
        }

        return new EmailSettings
        {
            SmtpHost = stored.SmtpHost,
            SmtpPort = stored.SmtpPort,
            EnableSsl = stored.EnableSsl,
            Username = stored.Username ?? string.Empty,
            Password = password ?? string.Empty,
            FromAddress = stored.FromAddress,
            FromName = stored.FromName,
            DashboardBaseUrl = stored.DashboardBaseUrl,
            SupportEmail = stored.SupportEmail,
            SupportPhone = stored.SupportPhone,
            TimeZoneId = string.IsNullOrWhiteSpace(stored.TimeZoneId)
                ? fallback.Value.TimeZoneId
                : stored.TimeZoneId
        };
    }
}
