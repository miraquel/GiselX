using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IStockUploadService
{
    Task<ServiceResponse<StockUploadPreflightDto>> PreflightAsync(Stream file, string originalFileName,
        DateTime stockDate, int companyId, CancellationToken cancellationToken);
    Task<ServiceResponse<StockUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        DateTime stockDate, int companyId, bool confirmed, string uploadedBy, string? confirmedSha256,
        CancellationToken cancellationToken);
    /// <summary>
    /// The workbook that was uploaded, scoped to the caller's company so one distributor cannot read
    /// another's file by guessing a batch id.
    /// </summary>
    Task<ServiceResponse<ArchivedUploadFileDto>> DownloadSourceAsync(long batchId, int companyId,
        CancellationToken cancellationToken);

    Task<ServiceResponse<StockUploadBatchDto>> GetResultAsync(long batchId, int companyId,
        CancellationToken cancellationToken);
}
