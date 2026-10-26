namespace GiselX.Service.Dto;

public sealed class UnmappedItemCompanySummaryDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLocation { get; set; }
    public int UnmappedItemCount { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTime LastSeenDate { get; set; }
}
