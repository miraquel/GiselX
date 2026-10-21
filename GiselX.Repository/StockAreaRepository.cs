using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public sealed class StockAreaRepository(IDbConnection connection, IDbTransaction transaction)
    : IStockAreaRepository
{
    private const string Columns =
        "Id, CompanyId, Name, Aliases, IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy";

    public async Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly,
        CancellationToken cancellationToken)
    {
        var values = await connection.QueryAsync<StockArea>(new CommandDefinition($"""
            SELECT {Columns}
            FROM StockArea
            WHERE CompanyId = @CompanyId AND (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY Name;
            """, new { CompanyId = companyId, ActiveOnly = activeOnly }, transaction,
            cancellationToken: cancellationToken));
        return values.ToList();
    }

    public Task<StockArea?> GetAsync(int id, int companyId, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<StockArea>(new CommandDefinition($"""
            SELECT {Columns} FROM StockArea WHERE Id = @Id AND CompanyId = @CompanyId;
            """, new { Id = id, CompanyId = companyId }, transaction, cancellationToken: cancellationToken));

    public async Task<int> InsertAsync(StockArea area, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO StockArea (CompanyId, Name, Aliases, IsActive, CreatedDate, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES (@CompanyId, @Name, @Aliases, @IsActive, @CreatedDate, @CreatedBy);
                """, area, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new StockAreaDuplicateException(exception);
        }
    }

    public async Task UpdateAsync(StockArea area, CancellationToken cancellationToken)
    {
        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE StockArea
                SET Name = @Name, Aliases = @Aliases, IsActive = @IsActive,
                    ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
                WHERE Id = @Id AND CompanyId = @CompanyId;
                """, area, transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("The area was not found.");
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new StockAreaDuplicateException(exception);
        }
    }

    public async Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy,
        DateTime modifiedDate, CancellationToken cancellationToken)
    {
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE StockArea
            SET IsActive = @IsActive, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
            WHERE Id = @Id AND CompanyId = @CompanyId;
            """, new { Id = id, CompanyId = companyId, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            transaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException("The area was not found.");
    }

    public async Task<bool> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM StockArea
            WHERE Id = @Id AND CompanyId = @CompanyId
              AND NOT EXISTS (SELECT 1 FROM Stock WHERE StockAreaId = @Id)
              AND NOT EXISTS (SELECT 1 FROM StockStaging WHERE StockAreaId = @Id);
            """, new { Id = id, CompanyId = companyId }, transaction,
            cancellationToken: cancellationToken)) == 1;
}
