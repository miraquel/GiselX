namespace GiselX.Service;

public sealed class SelloutUploadOptions
{
    public const string SectionName = "SelloutUpload";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// The share of parsed rows that may fail validation before the whole upload is refused. Below it the
    /// valid rows publish and the rest are reported; at or above it nothing is published, because a file
    /// that broken is a mapping or export problem rather than a few bad cells. Zero restores the older
    /// all-or-nothing behaviour.
    /// </summary>
    public decimal MaxInvalidRowPercentage { get; set; } = 5m;

    /// <summary>
    /// The most days, counted inclusively from the earliest to the latest invoice date of the valid rows, one
    /// upload may replace. Publication deletes every published row in that range, so a single mistyped year
    /// would otherwise wipe months of sales; a workbook beyond the limit is refused with the two dates named.
    /// Zero or less disables the check.
    /// </summary>
    public int MaxReplacementDays { get; set; } = 45;
}
