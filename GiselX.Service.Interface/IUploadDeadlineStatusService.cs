using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface IUploadDeadlineStatusService
{
    /// <summary>The company's next deadline and what is still missing for it; null without a schedule.</summary>
    Task<UploadDeadlineStatusDto?> GetAsync(int companyId, CancellationToken cancellationToken);
}
