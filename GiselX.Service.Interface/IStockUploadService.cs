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
    Task<ServiceResponse<StockUploadBatchDto>> GetResultAsync(long batchId, int companyId,
        CancellationToken cancellationToken);
}
