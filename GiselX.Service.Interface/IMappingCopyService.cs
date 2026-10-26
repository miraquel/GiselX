using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

/// <summary>
/// Copies template and item mapping definitions from one company to another — typically from one location of a
/// distributor to a new one. Copies only add: nothing the target already has is updated or removed.
/// </summary>
public interface IMappingCopyService
{
    Task<IReadOnlyList<CopySourceCompanyDto>> ListSourceCompaniesAsync(int targetCompanyId,
        CancellationToken cancellationToken);

    Task<ServiceResponse<MappingCopyPreviewDto>> PreviewAsync(int sourceCompanyId, int targetCompanyId,
        CancellationToken cancellationToken);

    Task<ServiceResponse<MappingCopyResultDto>> CopyAsync(MappingCopyRequestDto request, string username,
        CancellationToken cancellationToken);
}
