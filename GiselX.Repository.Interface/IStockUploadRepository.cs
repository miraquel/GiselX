using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IStockUploadRepository
{
    Task<long> InsertBatchAsync(StockUploadBatch batch, CancellationToken cancellationToken);
    Task InsertStagingAsync(IReadOnlyCollection<StockStaging> rows, CancellationToken cancellationToken);
    Task MarkFailedAsync(long batchId, string errorSummary, CancellationToken cancellationToken);

    /// <summary>Records where the uploaded workbook was archived, once the batch id is known.</summary>
    Task SetArchivedFilePathAsync(long batchId, string archivedFilePath, CancellationToken cancellationToken);
    Task PublishValidRowsAsync(long batchId, int companyId, StockUploadBatchStatus status,
        CancellationToken cancellationToken);

    /// <summary>A <paramref name="companyId"/> of 0 matches any company — the Administrator scope.</summary>
    Task<(StockUploadBatch Batch, IReadOnlyList<StockStaging> Rows)?> GetBatchAsync(long batchId, int companyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// What is already published for the company on <paramref name="stockDate"/>: one entry per area
    /// per live batch. Read-only, so a preview can call it.
    /// </summary>
    Task<IReadOnlyList<PublishedStockArea>> GetPublishedAreasAsync(int companyId, DateTime stockDate,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
