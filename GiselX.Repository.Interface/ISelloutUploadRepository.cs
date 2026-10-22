using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface ISelloutUploadRepository
{
    Task<long> InsertGroupAsync(SelloutUploadGroup group, CancellationToken cancellationToken);
    Task<long> InsertBatchAsync(SelloutUploadBatch batch, CancellationToken cancellationToken);
    Task InsertStagingAsync(IReadOnlyCollection<SelloutStaging> rows, CancellationToken cancellationToken);
    Task MarkGroupFailedAsync(long groupId, int totalRows, int invalidRows, string errorSummary,
        string validationErrorsJson, CancellationToken cancellationToken);
    Task PromoteGroupAsync(long groupId, CancellationToken cancellationToken);

    /// <summary>
    /// The company's live batches (status 2 or 4) with the number of their published rows whose invoice date
    /// falls on <c>from</c> through <c>to</c> inclusive - the same range <see cref="PromoteGroupAsync"/> replaces.
    /// </summary>
    Task<IReadOnlyList<PublishedSelloutBatch>> GetPublishedInRangeAsync(int companyId, DateTime from, DateTime to,
        CancellationToken cancellationToken);

    /// <summary>Records where the uploaded workbook was archived, once the group id is known.</summary>
    Task SetArchivedFilePathAsync(long groupId, string archivedFilePath, CancellationToken cancellationToken);

    /// <summary>
    /// For both reads a <c>companyId</c> of 0 matches any company — the Administrator scope.
    /// </summary>
    Task<(SelloutUploadGroup Group, IReadOnlyList<SelloutUploadBatch> Batches, IReadOnlyList<SelloutStaging> Rows)?> GetGroupAsync(
        long groupId, int companyId, CancellationToken cancellationToken);
    Task<(SelloutUploadBatch Batch, IReadOnlyList<SelloutStaging> Rows)?> GetBatchAsync(long batchId, int companyId, CancellationToken cancellationToken);
}
