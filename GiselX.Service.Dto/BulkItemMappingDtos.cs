namespace GiselX.Service.Dto;

public sealed class BulkItemMappingResultDto
{
    public int SavedCount { get; set; }
    public List<BulkItemMappingRowErrorDto> RowErrors { get; set; } = [];
}

public sealed class BulkItemMappingRowErrorDto
{
    public string DistributorItemId { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
}

public sealed class GmkItemSuggestionDto
{
    public string GmkItemId { get; set; } = string.Empty;
    public string GmkItemName { get; set; } = string.Empty;
}
