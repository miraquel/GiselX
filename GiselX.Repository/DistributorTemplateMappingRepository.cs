using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public sealed class DistributorTemplateMappingRepository : IDistributorTemplateMappingRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public DistributorTemplateMappingRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public Task<IReadOnlyList<DistributorTemplateMapping>> GetByCompanyAsync(int companyId,
        CancellationToken cancellationToken) => LoadAsync(companyId, activeOnly: false, cancellationToken);

    public Task<IReadOnlyList<DistributorTemplateMapping>> GetActiveByCompanyAsync(int companyId,
        CancellationToken cancellationToken) => LoadAsync(companyId, activeOnly: true, cancellationToken);

    public async Task<DistributorTemplateMapping?> GetByIdAsync(int id, int companyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT * FROM DistributorTemplateMapping
            WHERE Id = @Id AND CompanyId = @CompanyId;
            SELECT c.* FROM DistributorTemplateColumn c
            INNER JOIN DistributorTemplateMapping m ON m.Id = c.DistributorTemplateMappingId
            WHERE m.Id = @Id AND m.CompanyId = @CompanyId
            ORDER BY c.SourceColumnNumber;
            """;

        var command = new CommandDefinition(sql, new { Id = id, CompanyId = companyId }, _dbTransaction,
            cancellationToken: cancellationToken);
        using var result = await _dbConnection.QueryMultipleAsync(command);
        var mapping = await result.ReadSingleOrDefaultAsync<DistributorTemplateMapping>();
        var columns = (await result.ReadAsync<DistributorTemplateColumn>()).ToList();
        if (mapping is not null)
        {
            mapping.Columns = columns;
        }

        return mapping;
    }

    public async Task<int> InsertAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken)
    {
        const string mappingSql = """
            INSERT INTO DistributorTemplateMapping
                (CompanyId, Name, WorksheetName, HeaderRowNumber, FirstDataRowNumber, HeaderSignature,
                 IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@CompanyId, @Name, @WorksheetName, @HeaderRowNumber, @FirstDataRowNumber, @HeaderSignature,
                 @IsActive, @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy);
            """;
        var command = new CommandDefinition(mappingSql, mapping, _dbTransaction,
            cancellationToken: cancellationToken);
        var id = await _dbConnection.ExecuteScalarAsync<int>(command);
        await InsertColumnsAsync(id, mapping.Columns, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE DistributorTemplateMapping
            SET Name = @Name,
                WorksheetName = @WorksheetName,
                HeaderRowNumber = @HeaderRowNumber,
                FirstDataRowNumber = @FirstDataRowNumber,
                HeaderSignature = @HeaderSignature,
                ModifiedDate = @ModifiedDate,
                ModifiedBy = @ModifiedBy
            WHERE Id = @Id AND CompanyId = @CompanyId;
            DELETE FROM DistributorTemplateColumn WHERE DistributorTemplateMappingId = @Id;
            """;
        var command = new CommandDefinition(sql, mapping, _dbTransaction, cancellationToken: cancellationToken);
        var affected = await _dbConnection.ExecuteAsync(command);
        if (affected == 0)
        {
            throw new InvalidOperationException("The distributor template mapping was not found.");
        }

        await InsertColumnsAsync(mapping.Id, mapping.Columns, cancellationToken);
    }

    public async Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE DistributorTemplateMapping
            SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate
            WHERE Id = @Id AND CompanyId = @CompanyId;
            """;
        var command = new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            _dbTransaction, cancellationToken: cancellationToken);
        if (await _dbConnection.ExecuteAsync(command) == 0)
        {
            throw new InvalidOperationException("The distributor template mapping was not found.");
        }
    }

    public async Task<DistributorTemplateMappingDeleteResult> DeleteAsync(int id, int companyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE mapping
            FROM DistributorTemplateMapping mapping
            WHERE mapping.Id = @Id
              AND mapping.CompanyId = @CompanyId
              AND NOT EXISTS (
                  SELECT 1
                  FROM SelloutUploadBatch batch
                  WHERE batch.DistributorTemplateMappingId = mapping.Id);

            SELECT CASE
                WHEN @@ROWCOUNT = 1 THEN 1
                WHEN EXISTS (
                    SELECT 1
                    FROM DistributorTemplateMapping mapping
                    INNER JOIN SelloutUploadBatch batch
                        ON batch.DistributorTemplateMappingId = mapping.Id
                    WHERE mapping.Id = @Id AND mapping.CompanyId = @CompanyId)
                    THEN 2
                ELSE 0
            END;
            """;
        var command = new CommandDefinition(sql, new { Id = id, CompanyId = companyId }, _dbTransaction,
            cancellationToken: cancellationToken);
        return (DistributorTemplateMappingDeleteResult)
            await _dbConnection.ExecuteScalarAsync<int>(command);
    }

    private async Task<IReadOnlyList<DistributorTemplateMapping>> LoadAsync(int companyId, bool activeOnly,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT * FROM DistributorTemplateMapping
            WHERE CompanyId = @CompanyId AND (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY Name;
            SELECT c.* FROM DistributorTemplateColumn c
            INNER JOIN DistributorTemplateMapping m ON m.Id = c.DistributorTemplateMappingId
            WHERE m.CompanyId = @CompanyId AND (@ActiveOnly = 0 OR m.IsActive = 1)
            ORDER BY c.DistributorTemplateMappingId, c.SourceColumnNumber;
            """;
        var command = new CommandDefinition(sql, new { CompanyId = companyId, ActiveOnly = activeOnly },
            _dbTransaction, cancellationToken: cancellationToken);
        using var result = await _dbConnection.QueryMultipleAsync(command);
        var mappings = (await result.ReadAsync<DistributorTemplateMapping>()).ToList();
        var columns = (await result.ReadAsync<DistributorTemplateColumn>())
            .GroupBy(column => column.DistributorTemplateMappingId)
            .ToDictionary(group => group.Key, group => (ICollection<DistributorTemplateColumn>)group.ToList());
        foreach (var mapping in mappings)
        {
            mapping.Columns = columns.GetValueOrDefault(mapping.Id) ?? new List<DistributorTemplateColumn>();
        }

        return mappings;
    }

    private async Task InsertColumnsAsync(int mappingId, IEnumerable<DistributorTemplateColumn> columns,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO DistributorTemplateColumn
                (DistributorTemplateMappingId, SourceColumnNumber, SourceHeader, CanonicalField)
            VALUES (@DistributorTemplateMappingId, @SourceColumnNumber, @SourceHeader, @CanonicalField);
            """;
        var parameters = columns.Select(column => new
        {
            DistributorTemplateMappingId = mappingId,
            column.SourceColumnNumber,
            column.SourceHeader,
            CanonicalField = (int)column.CanonicalField
        });
        var command = new CommandDefinition(sql, parameters, _dbTransaction, cancellationToken: cancellationToken);
        await _dbConnection.ExecuteAsync(command);
    }
}
