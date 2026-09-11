namespace GiselX.Domain;

/// <summary>
/// A distributor item id seen in an upload that has no ItemMapping row yet. Derived from staging
/// history rather than stored, so it disappears on its own once a mapping is created.
/// </summary>
public sealed class UnmappedDistributorItem
{
    public string DistributorItemId { get; set; } = string.Empty;
    public string? DistributorItemName { get; set; }
    public bool SeenInStock { get; set; }
    public bool SeenInSellout { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTime FirstSeenDate { get; set; }
    public DateTime LastSeenDate { get; set; }
}
