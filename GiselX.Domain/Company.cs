namespace GiselX.Domain;

public partial class Company
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public virtual ICollection<TransDist> TransDist { get; set; } = new List<TransDist>();
    public virtual ICollection<AppIdentityUser> Users { get; set; } = new List<AppIdentityUser>();
}
