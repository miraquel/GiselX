using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

/// <summary>
/// Opens its own connection rather than joining the ambient transaction: the settings are read by
/// the mailer, which runs inside a Hangfire job whose scope stays alive for the whole SMTP exchange.
/// </summary>
public sealed class EmailSettingRepository(IStandaloneConnectionFactory connectionFactory)
    : IEmailSettingRepository
{
    private const string Columns = """
        Id, SmtpHost, SmtpPort, EnableSsl, Username, PasswordProtected, FromAddress, FromName,
        DashboardBaseUrl, SupportEmail, SupportPhone, TimeZoneId, ModifiedDate, ModifiedBy
        """;

    public async Task<EmailSetting?> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<EmailSetting>(new CommandDefinition(
            $"SELECT {Columns} FROM EmailSetting WHERE Id = @Id;",
            new { Id = EmailSetting.SingletonId }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// One row, upserted. MERGE is avoided deliberately — this is a single known key, and the
    /// UPDATE-then-INSERT shape is the one the rest of this codebase can be read against.
    /// </summary>
    public async Task SaveAsync(EmailSetting setting, CancellationToken cancellationToken)
    {
        setting.Id = EmailSetting.SingletonId;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE EmailSetting
            SET SmtpHost = @SmtpHost, SmtpPort = @SmtpPort, EnableSsl = @EnableSsl,
                Username = @Username, PasswordProtected = @PasswordProtected,
                FromAddress = @FromAddress, FromName = @FromName,
                DashboardBaseUrl = @DashboardBaseUrl, SupportEmail = @SupportEmail,
                SupportPhone = @SupportPhone, TimeZoneId = @TimeZoneId,
                ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
            WHERE Id = @Id;

            IF @@ROWCOUNT = 0
                INSERT INTO EmailSetting (Id, SmtpHost, SmtpPort, EnableSsl, Username, PasswordProtected,
                    FromAddress, FromName, DashboardBaseUrl, SupportEmail, SupportPhone, TimeZoneId,
                    ModifiedDate, ModifiedBy)
                VALUES (@Id, @SmtpHost, @SmtpPort, @EnableSsl, @Username, @PasswordProtected,
                    @FromAddress, @FromName, @DashboardBaseUrl, @SupportEmail, @SupportPhone, @TimeZoneId,
                    @ModifiedDate, @ModifiedBy);
            """, setting, cancellationToken: cancellationToken));
    }
}
