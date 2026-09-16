using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

/// <summary>
/// Opens its own connection rather than joining the scoped ambient transaction, following
/// <see cref="PowerBiReportMenuRepository"/>. This runs inside a Hangfire job whose scope stays
/// alive for the whole SMTP exchange, and enlisting would hold a SQL transaction open across that
/// round trip for no benefit — the job only reads, and it reads nothing the request wrote.
/// </summary>
public sealed class UploadNotificationRepository(IStandaloneConnectionFactory connectionFactory)
    : IUploadNotificationRepository
{
    /// <summary>
    /// Every join is a LEFT JOIN because Company.BusinessUnitId, ChannelId and SalesmanId are all
    /// nullable: a company with none of them still notifies its own contact, just nobody internal.
    /// The three internal lists are returned separately rather than concatenated in SQL so the
    /// service can say which master a recipient came from when none of them yields an address.
    /// </summary>
    public async Task<UploadNotificationRecipients?> GetRecipientsAsync(int companyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                company.Id            AS CompanyId,
                company.Name          AS CompanyName,
                company.ContactEmail  AS ContactEmail,
                unit.Name             AS BusinessUnitName,
                unit.NotificationEmails AS InternalNotificationEmails,
                channel.Name          AS ChannelName,
                channel.NotificationEmails AS ChannelNotificationEmails,
                salesman.Name         AS SalesmanName,
                salesman.NotificationEmails AS SalesmanNotificationEmails
            FROM Company company
            LEFT JOIN BusinessUnit unit ON unit.Id = company.BusinessUnitId
            LEFT JOIN Channel channel  ON channel.Id = company.ChannelId
            LEFT JOIN Salesman salesman ON salesman.Id = company.SalesmanId
            WHERE company.Id = @CompanyId;
            """;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UploadNotificationRecipients>(
            new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken));
    }
}
