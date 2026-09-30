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

    public async Task<bool> HasDataForPeriodAsync(int companyId, DateTime fromLocal, DateTime toLocal,
        DateTime fromUtc, DateTime toUtc)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(1) FROM [dbo].[SalesTransaction] WHERE CompanyId = @CompanyId AND CreatedDate >= @FromUtc AND CreatedDate < @ToUtc),
                (SELECT COUNT(1) FROM [dbo].[Stock]            WHERE CompanyId = @CompanyId AND CreatedDate >= @FromUtc AND CreatedDate < @ToUtc),
                (SELECT COUNT(1) FROM [dbo].[TransDist]        WHERE CompanyId = @CompanyId AND CreatedDate >= @FromLocal AND CreatedDate < @ToLocal)
            """;

        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var result = await _dbConnection.QuerySingleAsync<(int Sales, int Stock, int TransDist)>(
            sql,
            new
            {
                CompanyId = companyId,
                FromLocal = fromLocal,
                ToLocal = toLocal,
                FromUtc = fromUtc,
                ToUtc = toUtc
            },
            _dbTransaction);

        return result.Sales > 0 && result.Stock > 0 && result.TransDist > 0;
    }
}
