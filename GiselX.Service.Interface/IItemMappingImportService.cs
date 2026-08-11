using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IItemMappingImportService
{
    Task<ServiceResponse<ItemMappingImportPreviewDto>> PreflightAsync(byte[] workbook, int companyId,
        CancellationToken cancellationToken);
    Task<ServiceResponse<ItemMappingImportResultDto>> ImportAsync(byte[] workbook, int companyId, bool confirmed,
        string username, CancellationToken cancellationToken);
}
