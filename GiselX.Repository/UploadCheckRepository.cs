using System.Data;
using Dapper;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public class UploadCheckRepository : IUploadCheckRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public UploadCheckRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public async Task<UploadPresence> GetUploadPresenceAsync(int companyId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SalesTransaction]
                                  WHERE CompanyId = @CompanyId AND CreatedDate >= @FromUtc AND CreatedDate < @ToUtc)
                     THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasSellout,
                CASE WHEN EXISTS (SELECT 1 FROM [dbo].[Stock]
                                  WHERE CompanyId = @CompanyId AND CreatedDate >= @FromUtc AND CreatedDate < @ToUtc)
                     THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasStock;

            SELECT area.Name
            FROM [dbo].[StockArea] area
            WHERE area.CompanyId = @CompanyId AND area.IsActive = 1
              AND NOT EXISTS (SELECT 1 FROM [dbo].[Stock] stock
                              WHERE stock.CompanyId = @CompanyId AND stock.StockAreaId = area.Id
                                AND stock.CreatedDate >= @FromUtc AND stock.CreatedDate < @ToUtc)
            ORDER BY area.Name;
            """;

        using var result = await _dbConnection.QueryMultipleAsync(new CommandDefinition(sql,
            new { CompanyId = companyId, FromUtc = fromUtc, ToUtc = toUtc }, _dbTransaction,
            cancellationToken: cancellationToken));
        var (hasSellout, hasStock) = await result.ReadSingleAsync<(bool, bool)>();
        var missingAreas = (await result.ReadAsync<string>()).ToList();
        return new UploadPresence(hasSellout, hasStock, missingAreas);
    }
}
