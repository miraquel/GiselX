using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public sealed class BusinessUnitRepository(IDbConnection connection, IDbTransaction transaction)
    : IBusinessUnitRepository
{
    public async Task<IReadOnlyList<BusinessUnit>> ListAsync(bool activeOnly,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Name, IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM BusinessUnit
            WHERE @ActiveOnly = 0 OR IsActive = 1
            ORDER BY Name;
            """;
        var values = await connection.QueryAsync<BusinessUnit>(new CommandDefinition(sql,
            new { ActiveOnly = activeOnly }, transaction, cancellationToken: cancellationToken));
        return values.ToList();
    }

    public Task<BusinessUnit?> GetAsync(int id, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<BusinessUnit>(new CommandDefinition("""
            SELECT Id, Name, IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy
            FROM BusinessUnit WHERE Id = @Id;
            """, new { Id = id }, transaction, cancellationToken: cancellationToken));

    public async Task<int> InsertAsync(BusinessUnit businessUnit, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO BusinessUnit (Name, IsActive, CreatedDate, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES (@Name, @IsActive, @CreatedDate, @CreatedBy);
                """, businessUnit, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new BusinessUnitDuplicateException(exception);
        }
    }

    public async Task UpdateAsync(BusinessUnit businessUnit, CancellationToken cancellationToken)
    {
        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE BusinessUnit
                SET Name = @Name, IsActive = @IsActive, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
                WHERE Id = @Id;
                """, businessUnit, transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("The business unit was not found.");
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new BusinessUnitDuplicateException(exception);
        }
    }

    public async Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken)
    {
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE BusinessUnit
            SET IsActive = @IsActive, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
            WHERE Id = @Id;
            """, new { Id = id, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            transaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException("The business unit was not found.");
    }

    public async Task<BusinessUnitDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE unit
            FROM BusinessUnit unit
            WHERE unit.Id = @Id
              AND NOT EXISTS (SELECT 1 FROM Company WHERE BusinessUnitId = unit.Id);

            SELECT CASE
                WHEN @@ROWCOUNT = 1 THEN 1
                WHEN EXISTS (SELECT 1 FROM BusinessUnit WHERE Id = @Id)
                    AND EXISTS (SELECT 1 FROM Company WHERE BusinessUnitId = @Id) THEN 2
                ELSE 0
            END;
            """;
        return (BusinessUnitDeleteResult)await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Id = id }, transaction, cancellationToken: cancellationToken));
    }
}
