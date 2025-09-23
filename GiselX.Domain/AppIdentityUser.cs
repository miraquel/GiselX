using Microsoft.AspNetCore.Identity;

namespace GiselX.Domain;

public class AppIdentityUser : IdentityUser
{
    [PersonalData]
    public string FirstName { get; set; } = string.Empty;

    [PersonalData]
    public string? LastName { get; set; } = string.Empty;

    public int CompanyId { get; set; }
    public virtual Company Company { get; set; } = null!;
}