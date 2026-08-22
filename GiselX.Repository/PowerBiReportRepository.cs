using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public sealed class PowerBiReportRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    : IPowerBiReportRepository
{
    public async Task<IReadOnlyList<PowerBiReport>> ListAdminAsync(int? companyId, CancellationToken token)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, EmbedUrl, DisplayOrder, IsActive,
                   CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM PowerBiReport
            WHERE (@CompanyId IS NULL OR CompanyId = @CompanyId)
            ORDER BY CompanyId, DisplayOrder, Name;
            """;
        var reports = await dbConnection.QueryAsync<PowerBiReport>(new CommandDefinition(sql, new { CompanyId = companyId },
            dbTransaction, cancellationToken: token));
        return reports.ToList();
    }

    public async Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, EmbedUrl, DisplayOrder, IsActive,
                   CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM PowerBiReport
            WHERE CompanyId = @CompanyId AND IsActive = 1
            ORDER BY DisplayOrder, Name;
            """;
        var reports = await dbConnection.QueryAsync<PowerBiReport>(new CommandDefinition(sql, new { CompanyId = companyId },
            dbTransaction, cancellationToken: token));
        return reports.ToList();
    }

    public Task<PowerBiReport?> GetAdminByIdAsync(int id, CancellationToken token)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, EmbedUrl, DisplayOrder, IsActive,
                   CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM PowerBiReport
            WHERE Id = @Id;
            """;
        return dbConnection.QuerySingleOrDefaultAsync<PowerBiReport>(new CommandDefinition(sql, new { Id = id },
            dbTransaction, cancellationToken: token));
    }

    public Task<PowerBiReport?> GetActiveByIdAndCompanyAsync(int id, int companyId, CancellationToken token)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, EmbedUrl, DisplayOrder, IsActive,
                   CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM PowerBiReport
            WHERE Id = @Id AND CompanyId = @CompanyId AND IsActive = 1;
            """;
        return dbConnection.QuerySingleOrDefaultAsync<PowerBiReport>(new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId }, dbTransaction, cancellationToken: token));
    }

    public Task<bool> NameExistsAsync(int companyId, string name, int? excludeId, CancellationToken token)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM PowerBiReport
                WHERE CompanyId = @CompanyId AND Name = @Name
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END AS bit);
            """;
        return dbConnection.ExecuteScalarAsync<bool>(new CommandDefinition(sql,
            new { CompanyId = companyId, Name = name, ExcludeId = excludeId }, dbTransaction, cancellationToken: token));
    }

    public Task<int> GetNextDisplayOrderAsync(int companyId, CancellationToken token)
    {
        const string sql = "SELECT COALESCE(MAX(DisplayOrder), 0) + 1 FROM PowerBiReport WHERE CompanyId = @CompanyId;";
        return dbConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { CompanyId = companyId }, dbTransaction,
            cancellationToken: token));
    }

    public async Task<int> InsertAsync(PowerBiReport report, CancellationToken token)
    {
        const string sql = """
            INSERT INTO PowerBiReport
                (CompanyId, Name, EmbedUrl, DisplayOrder, IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@CompanyId, @Name, @EmbedUrl, @DisplayOrder, @IsActive, @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy);
            """;
        try
        {
            return await dbConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql, report, dbTransaction,
                cancellationToken: token));
        }
        catch (SqlException exception) when (IsCompanyNameDuplicate(exception))
        {
            throw new PowerBiReportDuplicateNameException(exception);
        }
    }

    public async Task<bool> UpdateAsync(PowerBiReport report, CancellationToken token)
    {
        const string sql = """
            UPDATE PowerBiReport
            SET CompanyId = @CompanyId,
                Name = @Name,
                EmbedUrl = @EmbedUrl,
                DisplayOrder = @DisplayOrder,
                IsActive = @IsActive,
                ModifiedDate = @ModifiedDate,
                ModifiedBy = @ModifiedBy
            WHERE Id = @Id;
            """;
        try
        {
            return await dbConnection.ExecuteAsync(new CommandDefinition(sql, report, dbTransaction,
                cancellationToken: token)) > 0;
        }
        catch (SqlException exception) when (IsCompanyNameDuplicate(exception))
        {
            throw new PowerBiReportDuplicateNameException(exception);
        }
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken token)
    {
        const string sql = """
            UPDATE PowerBiReport
            SET IsActive = @IsActive,
                ModifiedBy = @ModifiedBy,
                ModifiedDate = @ModifiedDate
            WHERE Id = @Id;
            """;
        return await dbConnection.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate }, dbTransaction,
            cancellationToken: token)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken token)
    {
        const string sql = "DELETE FROM PowerBiReport WHERE Id = @Id;";
        return await dbConnection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, dbTransaction,
            cancellationToken: token)) > 0;
    }

    private static bool IsCompanyNameDuplicate(SqlException exception) =>
        exception.Number is 2601 or 2627 &&
        exception.Message.Contains("UX_PowerBiReport_CompanyId_Name", StringComparison.Ordinal);
}
