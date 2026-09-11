using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public sealed class SalesmanRepository(IDbConnection connection, IDbTransaction transaction)
    : ISalesmanRepository
{
    private const string Columns = "Id, Code, Name, IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy";

    public async Task<IReadOnlyList<Salesman>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {Columns}
            FROM Salesman
            WHERE @ActiveOnly = 0 OR IsActive = 1
            ORDER BY Code;
            """;
        var values = await connection.QueryAsync<Salesman>(new CommandDefinition(sql,
            new { ActiveOnly = activeOnly }, transaction, cancellationToken: cancellationToken));
        return values.ToList();
    }

    public Task<Salesman?> GetAsync(int id, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<Salesman>(new CommandDefinition(
            $"SELECT {Columns} FROM Salesman WHERE Id = @Id;",
            new { Id = id }, transaction, cancellationToken: cancellationToken));

    public async Task<int> InsertAsync(Salesman salesman, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Salesman (Code, Name, IsActive, CreatedDate, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES (@Code, @Name, @IsActive, @CreatedDate, @CreatedBy);
                """, salesman, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new SalesmanDuplicateException(exception);
        }
    }

    public async Task UpdateAsync(Salesman salesman, CancellationToken cancellationToken)
    {
        int affected;
        try
        {
            affected = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Salesman
                SET Code = @Code, Name = @Name, IsActive = @IsActive,
                    ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
                WHERE Id = @Id;
                """, salesman, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new SalesmanDuplicateException(exception);
        }
        if (affected == 0) throw new InvalidOperationException("The salesman was not found.");
    }

    public async Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken)
    {
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Salesman
            SET IsActive = @IsActive, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
            WHERE Id = @Id;
            """, new { Id = id, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            transaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException("The salesman was not found.");
    }

    public async Task<SalesmanDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE salesman
            FROM Salesman salesman
            WHERE salesman.Id = @Id
              AND NOT EXISTS (SELECT 1 FROM Company WHERE SalesmanId = salesman.Id);

            SELECT CASE
                WHEN @@ROWCOUNT = 1 THEN 1
                WHEN EXISTS (SELECT 1 FROM Salesman WHERE Id = @Id)
                    AND EXISTS (SELECT 1 FROM Company WHERE SalesmanId = @Id) THEN 2
                ELSE 0
            END;
            """;
        return (SalesmanDeleteResult)await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Id = id }, transaction, cancellationToken: cancellationToken));
    }
}
