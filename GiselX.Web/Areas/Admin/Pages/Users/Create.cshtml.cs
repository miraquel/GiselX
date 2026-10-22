using GiselX.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using GiselX.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.Create)]
public class CreateModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly RoleManager<AppIdentityRole> _roleManager;
    private readonly IAuthorizationService _authorizationService;

    public CreateModel(
        UserManager<AppIdentityUser> userManager,
        RoleManager<AppIdentityRole> roleManager,
        IAuthorizationService authorizationService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _authorizationService = authorizationService;
    }

    [BindProperty]
    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    [Required]
    [Display(Name = "Company")]
    public int CompanyId { get; set; }

    [BindProperty]
    [Display(Name = "Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    public List<RoleOption> AvailableRoles { get; set; } = [];

    /// <summary>
    /// Assigning a role is a separate permission from creating a user: without it, being allowed to
    /// add users would also mean being allowed to hand out Administrator. The selector is hidden and
    /// any posted roles are ignored when the current user does not hold it.
    /// </summary>
    public bool CanAssignRoles { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public sealed class RoleOption
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        await LoadRolesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRolesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Only roles that actually exist can be assigned, so a tampered post cannot invent one.
        var rolesToAssign = CanAssignRoles
            ? SelectedRoles
                .Where(role => AvailableRoles.Any(available =>
                    string.Equals(available.Name, role, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : [];

        var user = new AppIdentityUser
        {
            UserName = UserName,
            Email = Email,
            FirstName = FirstName,
            LastName = LastName,
            PhoneNumber = PhoneNumber,
            EmailConfirmed = true,
            PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(PhoneNumber),
            CompanyId = CompanyId
        };
        var result = await _userManager.CreateAsync(user, Password);
        if (result.Succeeded)
        {
            if (rolesToAssign.Count > 0)
            {
                var roleResult = await _userManager.AddToRolesAsync(user, rolesToAssign);
                if (!roleResult.Succeeded)
                {
                    // The account exists at this point, so say so rather than implying nothing
                    // happened — the roles can still be set from the Manage Roles screen. This is a
                    // partial failure, so it goes to ErrorMessage: Index renders StatusMessage green.
                    TempData["ErrorMessage"] =
                        $"User '{UserName}' was created, but the role(s) could not be assigned: " +
                        $"{string.Join("; ", roleResult.Errors.Select(e => e.Description))} " +
                        "Assign them from Manage Roles.";
                    return RedirectToPage("Index");
                }

                StatusMessage = $"User '{UserName}' created successfully with role(s): " +
                                $"{string.Join(", ", rolesToAssign)}.";
                return RedirectToPage("Index");
            }

            StatusMessage = "User created successfully.";
            return RedirectToPage("Index");
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return Page();
    }

    private async Task LoadRolesAsync()
    {
        CanAssignRoles = (await _authorizationService
            .AuthorizeAsync(User, PermissionConstants.Users.UserRoles)).Succeeded;

        if (!CanAssignRoles)
        {
            AvailableRoles = [];
            return;
        }

        AvailableRoles = await _roleManager.Roles
            .OrderBy(role => role.Name)
            .Select(role => new RoleOption
            {
                Name = role.Name ?? string.Empty,
                Description = role.Description ?? string.Empty
            })
            .ToListAsync();

        // Only an Administrator may create another Administrator (RoleAssignmentPolicy). Removing it from
        // the offered roles is enough: OnPostAsync assigns only roles that appear in AvailableRoles.
        var assignable = RoleAssignmentPolicy.Assignable(AvailableRoles.Select(role => role.Name),
            User.IsInRole(RoleAssignmentPolicy.AdministratorRole));
        AvailableRoles = AvailableRoles.Where(role => assignable.Contains(role.Name)).ToList();
    }
}
