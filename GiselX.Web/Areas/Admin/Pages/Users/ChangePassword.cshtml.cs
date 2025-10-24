using System.ComponentModel.DataAnnotations;
using GiselX.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using GiselX.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.ChangePassword)]
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly ILogger<ChangePasswordModel> _logger;

    public ChangePasswordModel(UserManager<AppIdentityUser> userManager, ILogger<ChangePasswordModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; }

    public class InputModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound("User ID is required.");
        }
        Id = id;
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{id}'.");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }
        if (string.IsNullOrEmpty(Id))
        {
            return NotFound("User ID is required.");
        }
        var user = await _userManager.FindByIdAsync(Id);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{Id}'.");
        }
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
        _logger.LogInformation("Admin reset password for user with ID '{Id}'.", Id);
        StatusMessage = "Password has been changed successfully.";
        return RedirectToPage(new { id = Id });
    }
}