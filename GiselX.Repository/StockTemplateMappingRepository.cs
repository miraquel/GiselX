using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public sealed class StockTemplateMappingRepository(
    IDbConnection dbConnection,
    IDbTransaction dbTransaction) : IStockTemplateMappingRepository
{
    public Task<IReadOnlyList<StockTemplateMapping>> GetByCompanyAsync(int companyId,
        CancellationToken cancellationToken) => LoadAsync(companyId, false, cancellationToken);

    public Task<IReadOnlyList<StockTemplateMapping>> GetActiveByCompanyAsync(int companyId,
        CancellationToken cancellationToken) => LoadAsync(companyId, true, cancellationToken);

    public async Task<StockTemplateMapping?> GetByIdAsync(int id, int companyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT * FROM StockTemplateMapping WHERE Id = @Id AND CompanyId = @CompanyId;
            SELECT c.* FROM StockTemplateColumn c
            INNER JOIN StockTemplateMapping m ON m.Id = c.StockTemplateMappingId
            WHERE m.Id = @Id AND m.CompanyId = @CompanyId
            ORDER BY c.SourceColumnNumber;
            """;
        using var result = await dbConnection.QueryMultipleAsync(new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId }, dbTransaction, cancellationToken: cancellationToken));
        var mapping = await result.ReadSingleOrDefaultAsync<StockTemplateMapping>();
        if (mapping is not null) mapping.Columns = (await result.ReadAsync<StockTemplateColumn>()).ToList();
        return mapping;
    }

    public async Task<int> InsertAsync(StockTemplateMapping mapping, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO StockTemplateMapping
                (CompanyId, Name, WorksheetName, HeaderRowNumber, FirstDataRowNumber, HeaderSignature,
                 IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@CompanyId, @Name, @WorksheetName, @HeaderRowNumber, @FirstDataRowNumber, @HeaderSignature,
                 @IsActive, @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy);
            """;
        var id = await dbConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql, mapping, dbTransaction,
            cancellationToken: cancellationToken));
        await InsertColumnsAsync(id, mapping.Columns, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(StockTemplateMapping mapping, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE StockTemplateMapping SET Name = @Name, WorksheetName = @WorksheetName,
                HeaderRowNumber = @HeaderRowNumber, FirstDataRowNumber = @FirstDataRowNumber,
                HeaderSignature = @HeaderSignature, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
            WHERE Id = @Id AND CompanyId = @CompanyId;
            DELETE FROM StockTemplateColumn WHERE StockTemplateMappingId = @Id;
            """;
        if (await dbConnection.ExecuteAsync(new CommandDefinition(sql, mapping, dbTransaction,
                cancellationToken: cancellationToken)) == 0)
            throw new InvalidOperationException("The Stock template mapping was not found.");
        await InsertColumnsAsync(mapping.Id, mapping.Columns, cancellationToken);
    }

    public async Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy,
        DateTime modifiedDate, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE StockTemplateMapping SET IsActive = @IsActive, ModifiedBy = @ModifiedBy,
                ModifiedDate = @ModifiedDate WHERE Id = @Id AND CompanyId = @CompanyId;
            """;
        var affected = await dbConnection.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            dbTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException("The Stock template mapping was not found.");
    }

    public async Task<StockTemplateMappingDeleteResult> DeleteAsync(int id, int companyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE mapping FROM StockTemplateMapping mapping
            WHERE mapping.Id = @Id AND mapping.CompanyId = @CompanyId
              AND NOT EXISTS (SELECT 1 FROM StockUploadBatch batch WHERE batch.StockTemplateMappingId = mapping.Id);
            SELECT CASE WHEN @@ROWCOUNT = 1 THEN 1
                WHEN EXISTS (SELECT 1 FROM StockTemplateMapping mapping
                    INNER JOIN StockUploadBatch batch ON batch.StockTemplateMappingId = mapping.Id
                    WHERE mapping.Id = @Id AND mapping.CompanyId = @CompanyId) THEN 2 ELSE 0 END;
            """;
        var value = await dbConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId }, dbTransaction, cancellationToken: cancellationToken));
        return (StockTemplateMappingDeleteResult)value;
    }

    private async Task<IReadOnlyList<StockTemplateMapping>> LoadAsync(int companyId, bool activeOnly,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT * FROM StockTemplateMapping
            WHERE CompanyId = @CompanyId AND (@ActiveOnly = 0 OR IsActive = 1) ORDER BY Name;
            SELECT c.* FROM StockTemplateColumn c
            INNER JOIN StockTemplateMapping m ON m.Id = c.StockTemplateMappingId
            WHERE m.CompanyId = @CompanyId AND (@ActiveOnly = 0 OR m.IsActive = 1)
            ORDER BY c.StockTemplateMappingId, c.SourceColumnNumber;
            """;
        using var result = await dbConnection.QueryMultipleAsync(new CommandDefinition(sql,
            new { CompanyId = companyId, ActiveOnly = activeOnly }, dbTransaction,
            cancellationToken: cancellationToken));
        var mappings = (await result.ReadAsync<StockTemplateMapping>()).ToList();
        var columns = (await result.ReadAsync<StockTemplateColumn>())
            .GroupBy(column => column.StockTemplateMappingId)
            .ToDictionary(group => group.Key, group => (ICollection<StockTemplateColumn>)group.ToList());
        foreach (var mapping in mappings)
            mapping.Columns = columns.GetValueOrDefault(mapping.Id) ?? new List<StockTemplateColumn>();
        return mappings;
    }

    private Task InsertColumnsAsync(int mappingId, IEnumerable<StockTemplateColumn> columns,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO StockTemplateColumn
                (StockTemplateMappingId, SourceColumnNumber, SourceHeader, CanonicalField)
            VALUES (@StockTemplateMappingId, @SourceColumnNumber, @SourceHeader, @CanonicalField);
            """;
        var values = columns.Select(column => new
        {
            StockTemplateMappingId = mappingId,
            column.SourceColumnNumber,
            column.SourceHeader,
            CanonicalField = (int)column.CanonicalField
        });
        return dbConnection.ExecuteAsync(new CommandDefinition(sql, values, dbTransaction,
            cancellationToken: cancellationToken));
    }
}
