namespace GiselX.Service.Dto;

/// <summary>Which upload pipeline finished, so the mail can name it the way the screens do.</summary>
public enum UploadNotificationKind
{
    Sellout = 0,
    Stock = 1,
    ServiceLevel = 2
}

/// <summary>
/// Everything the completion mail needs, captured at the moment the upload committed. It is a flat
/// payload because it is serialised into the Hangfire job store and read back in a later process —
/// nothing here may be a reference to live state.
/// </summary>
public sealed class UploadNotificationDto
{
    public int CompanyId { get; set; }

    public UploadNotificationKind Kind { get; set; }

    /// <summary>Batch id (Stock), group id (Sellout), or null for the legacy Service Level upload.</summary>
    public long? Reference { get; set; }

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    /// <summary>Username of whoever uploaded, as recorded on the batch.</summary>
    public string UploadedBy { get; set; } = string.Empty;

    public DateTime CompletedAtUtc { get; set; }

    public string? OriginalFileName { get; set; }

    /// <summary>
    /// Free-form period description shown in the mail — a snapshot date for Stock, the covered weeks
    /// for Sellout. Null when the pipeline has no period concept.
    /// </summary>
    public string? PeriodLabel { get; set; }
}
