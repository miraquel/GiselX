namespace GiselX.Domain;

public sealed class Channel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Internal PT. GMK addresses notified when a distributor assigned to this record completes an
    /// upload, delimited by ';'. Null means this record adds nobody to the notification.
    /// </summary>
    public string? NotificationEmails { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    public ICollection<Company> Companies { get; set; } = [];
}
