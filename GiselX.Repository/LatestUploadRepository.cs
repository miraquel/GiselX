using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

/// <summary>
/// Reads the most recent published upload so a test notification can be sent against something that
/// really exists — with a working result link and, when the file was archived, a working download
/// link. A sample with no batch behind it cannot carry either, which made the test screen unable to
/// check the one thing people most want to check.
///
/// Its own connection, like the other objects the mailer touches: this is read from a screen, but
/// the same summary is useful outside a request transaction.
/// </summary>
public sealed class LatestUploadRepository(IStandaloneConnectionFactory connectionFactory)
    : ILatestUploadRepository
{
    public async Task<LatestUploadSummary?> GetLatestStockAsync(int? companyId,
        CancellationToken cancellationToken)
    {
        // Status 2 = Completed, 3 = CompletedWithWarnings in the Stock enum.
        const string sql = """
            SELECT TOP 1 Id AS Reference, OriginalFileName, TotalRows, ValidRows, InvalidRows,
                   UploadedBy, CompletedDate, ArchivedFilePath, StockDate
            FROM StockUploadBatch
            WHERE (@CompanyId IS NULL OR CompanyId = @CompanyId) AND Status IN (2, 3)
            ORDER BY CompletedDate DESC, Id DESC;
            """;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LatestUploadSummary>(
            new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken));
    }

    public async Task<LatestUploadSummary?> GetLatestSelloutAsync(int? companyId,
        CancellationToken cancellationToken)
    {
        // The GROUP enum, not the batch one: 2 = Completed, 3 = CompletedWithWarnings here.
        const string sql = """
            SELECT TOP 1 Id AS Reference, OriginalFileName, TotalRows, ValidRows, InvalidRows,
                   UploadedBy, CompletedDate, ArchivedFilePath, CAST(NULL AS date) AS StockDate
            FROM SelloutUploadGroup
            WHERE (@CompanyId IS NULL OR CompanyId = @CompanyId) AND Status IN (2, 3)
            ORDER BY CompletedDate DESC, Id DESC;
            """;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LatestUploadSummary>(
            new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken));
    }
}
