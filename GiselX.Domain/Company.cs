namespace GiselX.Domain;

public partial class Company
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? ContactEmail { get; set; }

    public int? DeadlineDayOfMonth { get; set; }

    public WeekDays? DeadlineDaysOfWeek { get; set; }

    public int ReminderLeadDays { get; set; } = 3;

    public virtual ICollection<TransDist> TransDist { get; set; } = new List<TransDist>();
    public virtual ICollection<ServelDeliveryEx> ServelDeliveryEx { get; set; } = new List<ServelDeliveryEx>();
    public virtual ICollection<ServelReceiptEx> ServelReceiptEx { get; set; } = new List<ServelReceiptEx>();
    public virtual ICollection<Stock> Stock { get; set; } = new List<Stock>();
    public virtual ICollection<SalesTransaction> SalesTransaction { get; set; } = new List<SalesTransaction>();
    public virtual ICollection<AppIdentityUser> Users { get; set; } = new List<AppIdentityUser>();
}
