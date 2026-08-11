namespace GiselX.Service.Dto;

public sealed class ItemIdentityResolutionDto
{
    public string? DistributorItemId { get; init; }
    public string? DistributorItemName { get; init; }
    public string? GmkItemId { get; init; }
    public string? GmkItemName { get; init; }
    public int? ItemMappingId { get; init; }
    public string? ErrorReason { get; init; }
    public bool IsMapped => ItemMappingId.HasValue;
}
