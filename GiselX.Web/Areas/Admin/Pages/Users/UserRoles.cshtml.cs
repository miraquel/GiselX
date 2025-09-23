using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.UserRoles)]
public class UserRolesModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly RoleManager<AppIdentityRole> _roleManager;

    public UserRolesModel(UserManager<AppIdentityUser> userManager, RoleManager<AppIdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [BindProperty(SupportsGet = true)]
    public string UserId { get; set; } = string.Empty;

    public AppIdentityUser? UserEntity { get; set; }

    public List<RoleViewModel> Roles { get; set; } = [];

    [BindProperty]
    public List<string> SelectedRoles { get; set; } = [];

    public class RoleViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Assigned { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? userId)
    {
        UserId = userId ?? UserId;
        if (string.IsNullOrEmpty(UserId))
            return NotFound();

        UserEntity = await _userManager.FindByIdAsync(UserId);
        if (UserEntity == null)
            return NotFound();

        var roles = _roleManager.Roles.ToList();

        var userRoles = await _userManager.GetRolesAsync(UserEntity);
            
        Roles = roles.Select(r => new RoleViewModel
        {
            Id = r.Id,
            Name = r.Name ?? string.Empty,
            Description = r.Description ?? string.Empty,
            Assigned = r.Name != null && userRoles.Contains(r.Name)
        }).ToList();
            
        SelectedRoles = Roles.Where(r => r.Assigned).Select(r => r.Name).ToList();
            
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(UserId))
            return NotFound();

        UserEntity = await _userManager.FindByIdAsync(UserId);
        if (UserEntity == null)
            return NotFound();

        var userRoles = await _userManager.GetRolesAsync(UserEntity);

        var rolesToAdd = SelectedRoles.Except(userRoles).ToList();
        var rolesToRemove = userRoles.Except(SelectedRoles).ToList();

        if (rolesToAdd.Count != 0)
            await _userManager.AddToRolesAsync(UserEntity, rolesToAdd);

        if (rolesToRemove.Count != 0)
            await _userManager.RemoveFromRolesAsync(UserEntity, rolesToRemove);

        return RedirectToPage("./Index");
    }
}