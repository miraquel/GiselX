namespace GiselX.Service.Dto;

public sealed class UnmappedDistributorItemDto
{
    public string DistributorItemId { get; set; } = string.Empty;
    public string DistributorItemName { get; set; } = string.Empty;
    public bool SeenInStock { get; set; }
    public bool SeenInSellout { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTime FirstSeenDate { get; set; }
    public DateTime LastSeenDate { get; set; }
}
