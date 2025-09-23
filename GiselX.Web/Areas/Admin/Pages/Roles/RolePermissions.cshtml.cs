using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiselX.Web.Areas.Admin.Pages.Roles;

[Authorize(PermissionConstants.Roles.RolePermissions)]
public partial class RolePermissionsModel : PageModel
{
    private readonly RoleManager<AppIdentityRole> _roleManager;
    private readonly UserManager<AppIdentityUser> _userManager;

    public RolePermissionsModel(RoleManager<AppIdentityRole> roleManager, UserManager<AppIdentityUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    [BindProperty]
    public string RoleId { get; set; } = string.Empty;

    [BindProperty]
    public List<string> SelectedPermissions { get; set; } = [];

    public Dictionary<string, List<string>> GroupedPermissions { get; set; } = new();

    // Helper to display a friendly name for a permission string
    public string GetPermissionFriendlyName(string permission)
    {
        // Example: "User.Create" => "Create"
        // Example: "User.ManageAll" => "Manage All"
        if (string.IsNullOrWhiteSpace(permission))
            return permission;

        var parts = permission.Split('.');
        var last = parts.Length > 1 ? parts[1] : parts[0];

        // Insert space before capital letters and capitalize first letter
        var friendly = MyRegex().Replace(last, "$1 $2");
        friendly = MyRegex1().Replace(friendly, "$1 $2");
        return char.ToUpper(friendly[0]) + friendly[1..];
    }

    // Helper to display a friendly name for a group (parent)
    public string GetGroupFriendlyName(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return group;

        // Insert space before capital letters and capitalize first letter
        var friendly = MyRegex().Replace(group, "$1 $2");
        friendly = MyRegex1().Replace(friendly, "$1 $2");
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(friendly.Trim());
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        RoleId = id;
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
            return NotFound();

        GroupedPermissions = PermissionConstants.GetGroupedPermissions();

        var claims = await _roleManager.GetClaimsAsync(role);
        SelectedPermissions = claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var role = await _roleManager.FindByIdAsync(RoleId);
        if (role == null || string.IsNullOrEmpty(role.Name))
        {
            return NotFound();
        }

        // Get current permission claims
        var claims = await _roleManager.GetClaimsAsync(role);
        var currentPermissions = claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet();

        var selectedPermissions = SelectedPermissions.ToHashSet();

        // Determine permissions to add and remove
        var toAdd = selectedPermissions.Except(currentPermissions).ToList();
        var toRemove = currentPermissions.Except(selectedPermissions).ToList();

        // Add new permission claims
        foreach (var permission in toAdd)
        {
            await _roleManager.AddClaimAsync(role, new Claim("permission", permission));
        }

        // Remove unselected permission claims
        foreach (var claim in claims.Where(c => c.Type == "permission" && toRemove.Contains(c.Value)))
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        // Invalidate all logins for users in this role
        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
        foreach (var user in usersInRole)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return RedirectToPage("Index");
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex MyRegex();
    [GeneratedRegex("([A-Z])([A-Z][a-z])")]
    private static partial Regex MyRegex1();
}