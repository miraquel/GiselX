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
    Task<(StockUploadBatch Batch, IReadOnlyList<StockStaging> Rows)?> GetBatchAsync(long batchId, int companyId,
        CancellationToken cancellationToken);
}
