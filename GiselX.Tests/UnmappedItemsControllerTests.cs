using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class UnmappedItemsControllerTests
{
    [Fact]
    public async Task Index_shows_the_companies_with_unmapped_items()
    {
        var summary = new UnmappedItemCompanySummaryDto { CompanyId = 4, CompanyName = "PT ABC", UnmappedItemCount = 3 };
        var controller = new UnmappedItemsController(new SummaryOnlyService([summary]));

        var result = await controller.Index(CancellationToken.None);

        var model = Assert.IsAssignableFrom<IReadOnlyList<UnmappedItemCompanySummaryDto>>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Same(summary, Assert.Single(model));
    }

    private sealed class SummaryOnlyService(IReadOnlyList<UnmappedItemCompanySummaryDto> summaries) : IItemMappingService
    {
        public Task<ServiceResponse<IReadOnlyList<UnmappedItemCompanySummaryDto>>> ListUnmappedSummaryAsync(
            CancellationToken token) =>
            Task.FromResult(new ServiceResponse<IReadOnlyList<UnmappedItemCompanySummaryDto>> { Data = summaries });

        public Task<ServiceResponse<IReadOnlyList<ItemMappingDto>>> ListAsync(int companyId, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<ServiceResponse<UnmappedDistributorItemListDto>> ListUnmappedAsync(int companyId, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<ServiceResponse<int>> SaveAsync(SaveItemMappingDto dto, string username, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<ServiceResponse<BulkItemMappingResultDto>> SaveBulkAsync(int companyId,
            IReadOnlyList<SaveItemMappingDto> rows, string username, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<GmkItemSuggestionDto>> ListGmkItemSuggestionsAsync(CancellationToken token) =>
            throw new NotSupportedException();
        public Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username,
            CancellationToken token) => throw new NotSupportedException();
        public Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
