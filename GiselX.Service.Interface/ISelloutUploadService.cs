using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface ISelloutUploadService
{
    Task<ServiceResponse<SelloutUploadPreflightDto>> PreflightAsync(Stream file, string originalFileName,
        int companyId, CancellationToken cancellationToken);
    Task<ServiceResponse<SelloutUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        int companyId, bool multiWeekConfirmed, string uploadedBy, string? confirmedSha256,
        CancellationToken cancellationToken);
    Task<ServiceResponse<SelloutUploadGroupDto>> GetUploadResultAsync(long? groupId, long? batchId, int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<SelloutUploadBatchDto>> GetBatchAsync(long batchId, int companyId,
        CancellationToken cancellationToken);
}
