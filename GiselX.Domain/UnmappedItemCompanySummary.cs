namespace GiselX.Domain;

/// <summary>How many distinct unmapped distributor items one company has, across both upload pipelines.</summary>
public sealed class UnmappedItemCompanySummary
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLocation { get; set; }
    public int UnmappedItemCount { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTime LastSeenDate { get; set; }
}
