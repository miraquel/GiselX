using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public sealed class PowerBiReportMenuRepository(IDbConnection dbConnection) : IPowerBiReportMenuRepository
{
    public async Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, EmbedUrl, DisplayOrder, IsActive,
                   CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM PowerBiReport
            WHERE CompanyId = @CompanyId AND IsActive = 1
            ORDER BY DisplayOrder, Name;
            """;
        var reports = await dbConnection.QueryAsync<PowerBiReport>(new CommandDefinition(
            sql, new { CompanyId = companyId }, cancellationToken: token));
        return reports.ToList();
    }
}
