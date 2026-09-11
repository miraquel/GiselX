namespace GiselX.Service.Dto;

public sealed class SelloutWeekSummaryDto
{
    public DateTime ReportingWeekStart { get; set; }
    public int RowCount { get; set; }

    /// <summary>
    /// Earliest and latest invoice date this workbook contributes to the week. Compared against the
    /// workbook's overall range these show where a week reaches beyond what the file actually covers.
    /// </summary>
    public DateTime CoveredFrom { get; set; }
    public DateTime CoveredTo { get; set; }
    public DateTime ReportingWeekEnd => ReportingWeekStart.AddDays(6);
}
