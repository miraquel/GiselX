using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public sealed class StockUploadRepository(IDbConnection connection, IDbTransaction transaction) : IStockUploadRepository
{
    public Task<long> InsertBatchAsync(StockUploadBatch batch, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO StockUploadBatch (CompanyId, StockTemplateMappingId, StockDate, OriginalFileName,
             FileSha256, Status, TotalRows, ValidRows, InvalidRows, IgnoredSummaryRowsJson,
             ValidationErrorsJson, ErrorSummary, UploadedBy, UploadedDate, CompletedDate)
            OUTPUT INSERTED.Id VALUES (@CompanyId, @StockTemplateMappingId, @StockDate, @OriginalFileName,
             @FileSha256, @Status, @TotalRows, @ValidRows, @InvalidRows, @IgnoredSummaryRowsJson,
             @ValidationErrorsJson, NULL, @UploadedBy, @UploadedDate, NULL)
            """, batch, transaction, cancellationToken: cancellationToken));

    public Task SetArchivedFilePathAsync(long batchId, string archivedFilePath,
        CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition(
            "UPDATE StockUploadBatch SET ArchivedFilePath = @Path WHERE Id = @BatchId;",
            new { BatchId = batchId, Path = archivedFilePath }, transaction,
            cancellationToken: cancellationToken));

    public async Task InsertStagingAsync(IReadOnlyCollection<StockStaging> rows,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO StockStaging (StockUploadBatchId, StockTemplateMappingId, SourceWorksheetName,
             SourceRowNumber, ProductId, ProductName,
             DistributorProductId, DistributorProductName, ItemMappingId,
             ProductPackaging, ProductPcsInCtn, ProductNetto, ProductUnit, SmallestUnit, SmallestUnitQty,
             PriceUnit, SaldoAwal, SaldoMasukPO,
             SaldoAkhir, BatchNumber, ExpiredDate, IsValid, ValidationErrorsJson,
             StockAreaId, Condition, SourceAreaText, SourceConditionText)
            VALUES (@StockUploadBatchId, @StockTemplateMappingId, @SourceWorksheetName,
             @SourceRowNumber, @ProductId, @ProductName,
             @DistributorProductId, @DistributorProductName, @ItemMappingId,
             @ProductPackaging, @ProductPcsInCtn, @ProductNetto, @ProductUnit, @SmallestUnit, @SmallestUnitQty,
             @PriceUnit, @SaldoAwal, @SaldoMasukPO,
             @SaldoAkhir, @BatchNumber, @ExpiredDate, @IsValid, @ValidationErrorsJson,
             @StockAreaId, @Condition, @SourceAreaText, @SourceConditionText)
            """;
        foreach (var chunk in rows.Chunk(500))
            await connection.ExecuteAsync(new CommandDefinition(sql, chunk, transaction,
                cancellationToken: cancellationToken));
    }

    public Task MarkFailedAsync(long batchId, string errorSummary, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("""
            UPDATE StockUploadBatch SET Status=1, ErrorSummary=@ErrorSummary, CompletedDate=SYSUTCDATETIME()
            WHERE Id=@BatchId AND Status=0
            """, new { BatchId = batchId, ErrorSummary = errorSummary }, transaction,
            cancellationToken: cancellationToken));

    /// <summary>
    /// Publishes a batch's valid rows, replacing whatever an earlier upload published for the same
    /// company and snapshot date.
    ///
    /// Until this superseded, publication only inserted. Re-uploading a snapshot left both sets of
    /// rows in Stock, so every product appeared twice and any total was silently doubled — a stock
    /// position is a statement about one moment, and two statements about that moment cannot both
    /// be true. Sellout has always worked this way for reporting weeks; Stock now matches it.
    /// </summary>
    public Task PublishValidRowsAsync(long batchId, int companyId, StockUploadBatchStatus status,
        CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition("""
            DECLARE @StockDate date;
            SELECT @StockDate = StockDate FROM StockUploadBatch WITH (UPDLOCK, HOLDLOCK)
             WHERE Id=@BatchId AND CompanyId=@CompanyId AND Status=0;

            -- Rows first, then the batches they belong to: deleting by status afterwards would also
            -- catch batches replaced by some earlier upload, whose rows are already gone.
            DELETE stock FROM Stock stock
            JOIN StockUploadBatch prior ON prior.Id = stock.StockUploadBatchId
            WHERE @StockDate IS NOT NULL
              AND prior.CompanyId=@CompanyId AND prior.StockDate=@StockDate
              AND prior.Id<>@BatchId AND prior.Status IN (2,3)
              -- Only the areas this upload actually carries: a Binjai file must not erase Medan's
              -- stock for the same date. No area (NULL) counts as one value, so a company without
              -- areas still replaces its whole snapshot. Area ids are identities, never 0.
              AND EXISTS (SELECT 1 FROM StockStaging s
                          WHERE s.StockUploadBatchId=@BatchId AND s.IsValid=1
                            AND COALESCE(s.StockAreaId,0)=COALESCE(stock.StockAreaId,0));

            -- Status 4 is Replaced. Stock numbers it 4 because 3 is already CompletedWithWarnings
            -- here; the Sellout enum uses the two the other way round.
            UPDATE prior SET Status=4
            FROM StockUploadBatch prior
            WHERE @StockDate IS NOT NULL
              AND prior.CompanyId=@CompanyId AND prior.StockDate=@StockDate
              AND prior.Id<>@BatchId AND prior.Status IN (2,3)
              -- A batch that also carried other areas keeps those rows and so stays live.
              AND NOT EXISTS (SELECT 1 FROM Stock remaining WHERE remaining.StockUploadBatchId=prior.Id);

            INSERT INTO Stock (ProductId, ProductName, DistributorProductId, DistributorProductName, ItemMappingId,
             ProductPackaging, ProductPcsInCtn, ProductNetto,
             ProductUnit, SmallestUnit, SmallestUnitQty, PriceUnit, SaldoAwal, SaldoMasukPO, SaldoAkhir,
             BatchNumber, ExpiredDate,
             CompanyId, CreatedDate, StockUploadBatchId, StockDate, StockAreaId, Condition)
            SELECT COALESCE(s.ProductId,''), COALESCE(s.ProductName,''), s.DistributorProductId,
             s.DistributorProductName, s.ItemMappingId, COALESCE(s.ProductPackaging,''),
             COALESCE(s.ProductPcsInCtn,0), COALESCE(s.ProductNetto,0), COALESCE(s.ProductUnit,''),
             s.SmallestUnit, s.SmallestUnitQty, s.PriceUnit,
             COALESCE(s.SaldoAwal,0), COALESCE(s.SaldoMasukPO,0), s.SaldoAkhir,
             COALESCE(s.BatchNumber,''), COALESCE(s.ExpiredDate,CONVERT(date,'17530101')),
             b.CompanyId, SYSUTCDATETIME(), b.Id, b.StockDate, s.StockAreaId, s.Condition
            FROM StockStaging s JOIN StockUploadBatch b ON b.Id=s.StockUploadBatchId
            WHERE b.Id=@BatchId AND b.CompanyId=@CompanyId AND b.Status=0 AND s.IsValid=1;

            UPDATE StockUploadBatch SET Status=@Status, CompletedDate=SYSUTCDATETIME()
            WHERE Id=@BatchId AND CompanyId=@CompanyId AND Status=0;
            """, new { BatchId = batchId, CompanyId = companyId, Status = (int)status }, transaction,
            cancellationToken: cancellationToken));

    public async Task<(StockUploadBatch Batch, IReadOnlyList<StockStaging> Rows)?> GetBatchAsync(
        long batchId, int companyId, CancellationToken cancellationToken)
    {
        using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT b.*, m.Name MappingName FROM StockUploadBatch b
             LEFT JOIN StockTemplateMapping m ON m.Id=b.StockTemplateMappingId
             WHERE b.Id=@BatchId AND (@CompanyId=0 OR b.CompanyId=@CompanyId);
            SELECT s.* FROM StockStaging s JOIN StockUploadBatch b ON b.Id=s.StockUploadBatchId
             WHERE b.Id=@BatchId AND (@CompanyId=0 OR b.CompanyId=@CompanyId) ORDER BY s.SourceRowNumber;
            """, new { BatchId = batchId, CompanyId = companyId }, transaction,
            cancellationToken: cancellationToken));
        var batch = await result.ReadSingleOrDefaultAsync<StockUploadBatch>();
        return batch is null ? null : (batch, (await result.ReadAsync<StockStaging>()).ToList());
    }
}
