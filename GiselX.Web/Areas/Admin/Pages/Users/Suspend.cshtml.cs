using GiselX.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using GiselX.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.Suspend)]
public class SuspendModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;

    public SuspendModel(UserManager<AppIdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Lockout Enabled")]
    public bool LockoutEnabled { get; set; }

    [BindProperty]
    [Display(Name = "Lockout End")]
    [DataType(DataType.DateTime)]
    public DateTimeOffset? LockoutEnd { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        Id = user.Id;
        LockoutEnabled = user.LockoutEnabled;
        LockoutEnd = user.LockoutEnd;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _userManager.FindByIdAsync(Id);
        if (user == null) return NotFound();
        user.LockoutEnabled = LockoutEnabled;
        user.LockoutEnd = LockoutEnd;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            StatusMessage = "User suspension updated successfully.";
            return RedirectToPage("Index");
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return Page();
    }
}
