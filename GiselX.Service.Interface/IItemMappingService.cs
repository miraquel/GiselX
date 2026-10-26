using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IItemMappingService
{
    Task<ServiceResponse<IReadOnlyList<ItemMappingDto>>> ListAsync(int companyId, CancellationToken token);
    Task<ServiceResponse<UnmappedDistributorItemListDto>> ListUnmappedAsync(int companyId,
        CancellationToken token);
    Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(SaveItemMappingDto dto, string username, CancellationToken token);

    /// <summary>
    /// Saves many new mappings for one company at once. Rows without any GMK value are ignored; invalid rows are
    /// returned in <see cref="BulkItemMappingResultDto.RowErrors"/> while the valid ones are saved.
    /// </summary>
    Task<ServiceResponse<BulkItemMappingResultDto>> SaveBulkAsync(int companyId, IReadOnlyList<SaveItemMappingDto> rows,
        string username, CancellationToken token);

    Task<IReadOnlyList<GmkItemSuggestionDto>> ListGmkItemSuggestionsAsync(CancellationToken token);

    /// <summary>Every company with item mapping enabled that still has unmapped distributor items.</summary>
    Task<ServiceResponse<IReadOnlyList<UnmappedItemCompanySummaryDto>>> ListUnmappedSummaryAsync(CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token);
}
