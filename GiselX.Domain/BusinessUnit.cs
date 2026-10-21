namespace GiselX.Domain;

public sealed class BusinessUnit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Internal PT. GMK addresses notified when a distributor in this unit completes an upload,
    /// delimited by ';'. Null means nobody internal is notified for this unit.
    /// </summary>
    public string? NotificationEmails { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    public ICollection<Company> Companies { get; set; } = [];
}
