using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.Details)]
public class ViewModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly RoleManager<AppIdentityRole> _roleManager;

    public ViewModel(UserManager<AppIdentityUser> userManager, RoleManager<AppIdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool Active { get; set; }
    public string Roles { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        Id = user.Id;
        UserName = user.UserName ?? string.Empty;
        Email = user.Email ?? string.Empty;
        FirstName = user.FirstName;
        LastName = user.LastName ?? string.Empty;
        Active = !(user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow);
        Roles = string.Join(", ", roles);
        PhoneNumber = user.PhoneNumber;
        return Page();
    }
}