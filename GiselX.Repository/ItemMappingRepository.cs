using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public sealed class ItemMappingRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    : IItemMappingRepository
{
    public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
        LoadAsync(companyId, false, cancellationToken);

    public Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
        LoadAsync(companyId, true, cancellationToken);

    public Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM ItemMapping WHERE Id = @Id AND CompanyId = @CompanyId;";
        return dbConnection.QuerySingleOrDefaultAsync<ItemMapping>(new CommandDefinition(sql, new { Id = id, CompanyId = companyId },
            dbTransaction, cancellationToken: cancellationToken));
    }

    public async Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO ItemMapping
                (CompanyId, GmkItemId, GmkItemName, DistributorItemId, DistributorItemName, IsActive,
                 CreatedDate, CreatedBy, ModifiedDate, ModifiedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@CompanyId, @GmkItemId, @GmkItemName, @DistributorItemId, @DistributorItemName, @IsActive,
                 @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy);
            """;
        try
        {
            return await dbConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql, mapping, dbTransaction,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ItemMappingDuplicateException(exception);
        }
    }

    public async Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ItemMapping
            SET GmkItemId = @GmkItemId,
                GmkItemName = @GmkItemName,
                DistributorItemId = @DistributorItemId,
                DistributorItemName = @DistributorItemName,
                IsActive = @IsActive,
                ModifiedDate = @ModifiedDate,
                ModifiedBy = @ModifiedBy
            WHERE Id = @Id AND CompanyId = @CompanyId;
            """;
        int affected;
        try
        {
            affected = await dbConnection.ExecuteAsync(new CommandDefinition(sql, mapping, dbTransaction,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ItemMappingDuplicateException(exception);
        }
        if (affected == 0) throw new InvalidOperationException("The item mapping was not found.");
    }

    public async Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ItemMapping
            SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate
            WHERE Id = @Id AND CompanyId = @CompanyId;
            """;
        var affected = await dbConnection.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            dbTransaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException("The item mapping was not found.");
    }

    public async Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE mapping
            FROM ItemMapping mapping
            WHERE mapping.Id = @Id
              AND mapping.CompanyId = @CompanyId
              AND NOT EXISTS (SELECT 1 FROM Stock WHERE ItemMappingId = mapping.Id)
              AND NOT EXISTS (SELECT 1 FROM StockStaging WHERE ItemMappingId = mapping.Id)
              AND NOT EXISTS (SELECT 1 FROM SalesTransaction WHERE ItemMappingId = mapping.Id)
              AND NOT EXISTS (SELECT 1 FROM SelloutStaging WHERE ItemMappingId = mapping.Id);

            SELECT CASE
                WHEN @@ROWCOUNT = 1 THEN 1
                WHEN EXISTS (
                    SELECT 1
                    FROM ItemMapping mapping
                    WHERE mapping.Id = @Id
                      AND mapping.CompanyId = @CompanyId
                      AND (
                          EXISTS (SELECT 1 FROM Stock WHERE ItemMappingId = mapping.Id)
                          OR EXISTS (SELECT 1 FROM StockStaging WHERE ItemMappingId = mapping.Id)
                          OR EXISTS (SELECT 1 FROM SalesTransaction WHERE ItemMappingId = mapping.Id)
                          OR EXISTS (SELECT 1 FROM SelloutStaging WHERE ItemMappingId = mapping.Id)
                      )) THEN 2
                ELSE 0
            END;
            """;
        return (ItemMappingDeleteResult)await dbConnection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Id = id, CompanyId = companyId }, dbTransaction, cancellationToken: cancellationToken));
    }

    public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE ItemMapping AS target
            USING (VALUES (@CompanyId, @GmkItemId, @GmkItemName, LTRIM(RTRIM(@DistributorItemId)), @DistributorItemName,
                           @IsActive, @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy)) AS source
                (CompanyId, GmkItemId, GmkItemName, DistributorItemId, DistributorItemName, IsActive,
                 CreatedDate, CreatedBy, ModifiedDate, ModifiedBy)
            ON target.CompanyId = source.CompanyId
               AND UPPER(LTRIM(RTRIM(target.DistributorItemId))) = UPPER(LTRIM(RTRIM(source.DistributorItemId)))
            WHEN MATCHED THEN UPDATE SET
                GmkItemId = source.GmkItemId,
                GmkItemName = source.GmkItemName,
                DistributorItemName = source.DistributorItemName,
                IsActive = source.IsActive,
                ModifiedDate = source.ModifiedDate,
                ModifiedBy = source.ModifiedBy
            WHEN NOT MATCHED THEN INSERT
                (CompanyId, GmkItemId, GmkItemName, DistributorItemId, DistributorItemName, IsActive,
                 CreatedDate, CreatedBy, ModifiedDate, ModifiedBy)
            VALUES
                (source.CompanyId, source.GmkItemId, source.GmkItemName, source.DistributorItemId,
                 source.DistributorItemName, source.IsActive, source.CreatedDate, source.CreatedBy,
                 source.ModifiedDate, source.ModifiedBy);
            """;
        return dbConnection.ExecuteAsync(new CommandDefinition(sql, values, dbTransaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UnmappedDistributorItem>> GetUnmappedDistributorItemsAsync(int companyId,
        CancellationToken cancellationToken)
    {
        // Derived from staging history rather than stored: a row whose ItemMappingId stayed NULL is an item the
        // resolver could not translate. The NOT EXISTS drops ids that have since been mapped, so an entry leaves
        // this list the moment an ItemMapping row is created for it.
        const string sql = """
            WITH Discovered AS (
                SELECT UPPER(LTRIM(RTRIM(staging.DistributorProductId))) AS NormalizedItemId,
                       LTRIM(RTRIM(staging.DistributorProductId)) AS DistributorItemId,
                       LTRIM(RTRIM(staging.DistributorProductName)) AS DistributorItemName,
                       batch.UploadedDate AS UploadedDate,
                       staging.Id AS StagingRowId,
                       1 AS FromStock,
                       0 AS FromSellout
                FROM StockStaging staging
                INNER JOIN StockUploadBatch batch ON batch.Id = staging.StockUploadBatchId
                WHERE batch.CompanyId = @CompanyId
                  AND staging.ItemMappingId IS NULL
                  AND NULLIF(LTRIM(RTRIM(staging.DistributorProductId)), '') IS NOT NULL
                UNION ALL
                SELECT UPPER(LTRIM(RTRIM(staging.DistributorProductId))),
                       LTRIM(RTRIM(staging.DistributorProductId)),
                       LTRIM(RTRIM(staging.DistributorProductName)),
                       batch.UploadedDate,
                       staging.Id,
                       0,
                       1
                FROM SelloutStaging staging
                INNER JOIN SelloutUploadBatch batch ON batch.Id = staging.SelloutUploadBatchId
                WHERE batch.CompanyId = @CompanyId
                  AND staging.ItemMappingId IS NULL
                  AND NULLIF(LTRIM(RTRIM(staging.DistributorProductId)), '') IS NOT NULL
            ),
            Ranked AS (
                SELECT discovered.*,
                       ROW_NUMBER() OVER (PARTITION BY discovered.NormalizedItemId
                                          ORDER BY discovered.UploadedDate DESC, discovered.StagingRowId DESC) AS RecencyRank
                FROM Discovered discovered
            )
            SELECT MAX(CASE WHEN ranked.RecencyRank = 1 THEN ranked.DistributorItemId END) AS DistributorItemId,
                   MAX(CASE WHEN ranked.RecencyRank = 1 THEN ranked.DistributorItemName END) AS DistributorItemName,
                   CAST(MAX(ranked.FromStock) AS bit) AS SeenInStock,
                   CAST(MAX(ranked.FromSellout) AS bit) AS SeenInSellout,
                   COUNT(*) AS OccurrenceCount,
                   MIN(ranked.UploadedDate) AS FirstSeenDate,
                   MAX(ranked.UploadedDate) AS LastSeenDate
            FROM Ranked ranked
            WHERE NOT EXISTS (
                SELECT 1
                FROM ItemMapping mapping
                WHERE mapping.CompanyId = @CompanyId
                  AND UPPER(LTRIM(RTRIM(mapping.DistributorItemId))) = ranked.NormalizedItemId)
            GROUP BY ranked.NormalizedItemId
            ORDER BY LastSeenDate DESC, DistributorItemId;
            """;
        var values = await dbConnection.QueryAsync<UnmappedDistributorItem>(new CommandDefinition(sql,
            new { CompanyId = companyId }, dbTransaction, cancellationToken: cancellationToken));
        return values.ToList();
    }

    public async Task<IReadOnlyList<UnmappedItemCompanySummary>> GetUnmappedSummaryByCompanyAsync(
        CancellationToken cancellationToken)
    {
        // The same definition as GetUnmappedDistributorItemsAsync, counted per company instead of listed per item,
        // so the overview always agrees with each company's Unmapped tab. Companies with item mapping switched off
        // are excluded inside each branch: every one of their staging rows has a NULL ItemMappingId.
        const string sql = """
            WITH Discovered AS (
                SELECT batch.CompanyId AS CompanyId,
                       UPPER(LTRIM(RTRIM(staging.DistributorProductId))) AS NormalizedItemId,
                       batch.UploadedDate AS UploadedDate
                FROM StockStaging staging
                INNER JOIN StockUploadBatch batch ON batch.Id = staging.StockUploadBatchId
                INNER JOIN Company company ON company.Id = batch.CompanyId AND company.ItemMappingEnabled = 1
                WHERE staging.ItemMappingId IS NULL
                  AND NULLIF(LTRIM(RTRIM(staging.DistributorProductId)), '') IS NOT NULL
                UNION ALL
                SELECT batch.CompanyId,
                       UPPER(LTRIM(RTRIM(staging.DistributorProductId))),
                       batch.UploadedDate
                FROM SelloutStaging staging
                INNER JOIN SelloutUploadBatch batch ON batch.Id = staging.SelloutUploadBatchId
                INNER JOIN Company company ON company.Id = batch.CompanyId AND company.ItemMappingEnabled = 1
                WHERE staging.ItemMappingId IS NULL
                  AND NULLIF(LTRIM(RTRIM(staging.DistributorProductId)), '') IS NOT NULL
            )
            SELECT company.Id AS CompanyId,
                   company.Name AS CompanyName,
                   company.Location AS CompanyLocation,
                   COUNT(DISTINCT discovered.NormalizedItemId) AS UnmappedItemCount,
                   COUNT(*) AS OccurrenceCount,
                   MAX(discovered.UploadedDate) AS LastSeenDate
            FROM Discovered discovered
            INNER JOIN Company company ON company.Id = discovered.CompanyId
            WHERE NOT EXISTS (
                SELECT 1
                FROM ItemMapping mapping
                WHERE mapping.CompanyId = discovered.CompanyId
                  AND UPPER(LTRIM(RTRIM(mapping.DistributorItemId))) = discovered.NormalizedItemId)
            GROUP BY company.Id, company.Name, company.Location
            ORDER BY LastSeenDate DESC, company.Name;
            """;
        var values = await dbConnection.QueryAsync<UnmappedItemCompanySummary>(new CommandDefinition(sql,
            transaction: dbTransaction, cancellationToken: cancellationToken));
        return values.ToList();
    }

    public async Task<IReadOnlyList<GmkItemSuggestion>> GetGmkItemSuggestionsAsync(CancellationToken cancellationToken)
    {
        // Deliberately across companies: the same GMK catalogue is mapped onto every distributor's codes.
        const string sql = """
            SELECT LTRIM(RTRIM(GmkItemId)) AS GmkItemId,
                   MAX(LTRIM(RTRIM(GmkItemName))) AS GmkItemName
            FROM ItemMapping
            WHERE IsActive = 1
              AND NULLIF(LTRIM(RTRIM(GmkItemId)), '') IS NOT NULL
            GROUP BY LTRIM(RTRIM(GmkItemId))
            ORDER BY GmkItemId;
            """;
        var values = await dbConnection.QueryAsync<GmkItemSuggestion>(new CommandDefinition(sql,
            transaction: dbTransaction, cancellationToken: cancellationToken));
        return values.ToList();
    }

    private async Task<IReadOnlyList<ItemMapping>> LoadAsync(int companyId, bool activeOnly,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT *
            FROM ItemMapping
            WHERE CompanyId = @CompanyId AND (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY DistributorItemId;
            """;
        var values = await dbConnection.QueryAsync<ItemMapping>(new CommandDefinition(sql,
            new { CompanyId = companyId, ActiveOnly = activeOnly }, dbTransaction, cancellationToken: cancellationToken));
        return values.ToList();
    }
}
