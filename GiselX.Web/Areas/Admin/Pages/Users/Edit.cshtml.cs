using GiselX.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using GiselX.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.Edit)]
public class EditModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;

    public EditModel(UserManager<AppIdentityUser> userManager)
    {
        _userManager = userManager;
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
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    [Required]
    [Display(Name = "Company")]
    public int CompanyId { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        UserName = user.UserName ?? string.Empty;
        Email = user.Email ?? string.Empty;
        FirstName = user.FirstName;
        LastName = user.LastName ?? string.Empty;
        PhoneNumber = user.PhoneNumber;
        CompanyId = user.CompanyId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (!ModelState.IsValid) return Page();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        user.UserName = UserName;
        user.Email = Email;
        user.FirstName = FirstName;
        user.LastName = LastName;
        user.PhoneNumber = PhoneNumber;
        user.PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(PhoneNumber);
        user.CompanyId = CompanyId;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            StatusMessage = "User updated successfully.";
            return RedirectToPage("Index");
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return Page();
    }
}
