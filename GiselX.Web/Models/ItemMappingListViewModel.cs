using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class ItemMappingListViewModel
{
    public int CompanyId { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public IReadOnlyList<ItemMappingDto> Mappings { get; set; } = [];
    public IReadOnlyList<UnmappedDistributorItemDto> UnmappedItems { get; set; } = [];
    public bool ItemMappingEnabled { get; set; }
    public string? ActiveTab { get; set; }
    public string? StatusMessage { get; set; }
    public IReadOnlyList<GmkItemSuggestionDto> GmkItems { get; set; } = [];

    /// <summary>Typed values and errors of a bulk save that is shown again, keyed by <see cref="UnmappedItemMappingInput.Key"/>.</summary>
    public IReadOnlyDictionary<string, UnmappedItemMappingInput> UnmappedInputs { get; set; } =
        new Dictionary<string, UnmappedItemMappingInput>();
}
