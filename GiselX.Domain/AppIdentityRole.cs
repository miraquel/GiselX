using Microsoft.AspNetCore.Identity;

namespace GiselX.Domain;

public class AppIdentityRole : IdentityRole
{
    public string Description { get; set; } = string.Empty;
}